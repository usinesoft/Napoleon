using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe;
using Napoleon.Server.PublishSubscribe.UdpImplementation;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace Napoleon.Server;

/// <summary>
/// Aggregates everything required to run on a server:
/// Instances of <see cref="IConsumer"/> and <see cref="IPublisher"/>
/// An instance of <see cref="Server"/> responsible for leader election and data synchronization
/// An instance of <see cref="DataServer"/> responsible for communication with clients
/// An instance of <see cref="DataStore"/> 
/// </summary>
public sealed class ServerSuite:IDisposable
{
    public ServerSuite(ILogger<ServerSuite> logger, IPersistenceEngine persistenceEngine)
    {
        _logger = logger;
        _persistenceEngine = persistenceEngine;
    }

    public DataStore Store { get; } = new DataStore();

    public Server? ClusterServer { get; private set; }

    public DataServer? DataServer { get; private set; }

    public NodeConfiguration? Config { get; private set; }

    private readonly ILogger<ServerSuite> _logger;
    private readonly IPersistenceEngine _persistenceEngine;

    /// <summary>
    /// Data may be stored as a single file or as a base file and a sequence of changes stored as Json
    /// </summary>
    /// <param name="dataDirectory"></param>
    private void LoadData(string dataDirectory)
    {
        _persistenceEngine.LoadData(Store, dataDirectory);
        
    }

    public void Start(NodeConfiguration config)
    {
        try
        {
            Config = config;

            if (config.DataDirectory != null)
            {
                if (!Directory.Exists(config.DataDirectory))
                {
                    Directory.CreateDirectory(config.DataDirectory);
                }

                LoadData(config.DataDirectory);
            
            }

            Store.BeforeDataChanged += SaveData;
        
            _logger.LogInformation("Starting notification publisher");

            var publisher = new Publisher(config.NetworkConfiguration.MulticastAddress!,
                config.NetworkConfiguration.MulticastPort);

            _logger.LogInformation("Starting notification consumer");
            var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
                config.NetworkConfiguration.MulticastPort);

        
            // this is the server responsible for cluster status (including data synchronization) and leader election
            ClusterServer = new Server(publisher, consumer, Store, config);

            _logger.LogInformation("Starting data server");
            // this is the server responsible for client requests and synchronization requests from other servers
            DataServer = new DataServer(Store, ClusterServer);

            int port = DataServer.Start(config.NetworkConfiguration.TcpClientPort);

            // in case the port is dynamic 
            ClusterServer.DataPort = port;

            _logger.LogInformation("Starting cluster coordinator");
            ClusterServer.Run();
        }
        catch (Exception e)
        {
            _logger.LogError("Error starting cluster node: {error}", e.Message);
            throw;
        }
    }

    private void SaveData(object? sender, DataChangedEventArgs e)
    {
        if (Config?.DataDirectory != null)
        {
            var change = e.ChangedData;
            var json = JsonSerializer.Serialize(change, SerializationContext.Default.Item);

            var fileName = $"change{change.Version:D5}.json";

            File.WriteAllText(Path.Combine(Config.DataDirectory, fileName),json);
        }
    }

    public void Dispose()
    {
        ClusterServer?.Dispose();
        DataServer?.Dispose();
    }
}