using Microsoft.Extensions.Logging;
using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace Napoleon.Server;

/// <summary>
///     Aggregates everything required to run on a server:
///     Instances of <see cref="IConsumer" /> and <see cref="IPublisher" />
///     An instance of <see cref="ClusterServer" /> responsible for leader election and data synchronization
///     An instance of <see cref="DataServer" /> responsible for communication with clients
///     An instance of <see cref="DataStore" />
/// </summary>
public sealed class ServerSuite : IDisposable
{
    public ClusterCoordinator ClusterServer { get; }
    private readonly ILogger<ServerSuite> _logger;
    private readonly IPersistenceEngine _persistenceEngine;


    public ServerSuite(ILogger<ServerSuite> logger, ClusterCoordinator clusterServer,
        IPersistenceEngine persistenceEngine, DataStore dataStore)
    {
        ClusterServer = clusterServer;
        _logger = logger;
        _persistenceEngine = persistenceEngine;
        Store = dataStore;
    }

    public DataStore Store { get; } 


    private DataServer? DataServer { get; set; }

    private NodeConfiguration? Config { get; set; }

    public void Dispose()
    {
        ClusterServer.Dispose();
        DataServer?.Dispose();
    }

    /// <summary>
    ///     Data may be stored as a single file or as a base file and a sequence of changes stored as Json
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
                if (!Directory.Exists(config.DataDirectory)) Directory.CreateDirectory(config.DataDirectory);

                LoadData(config.DataDirectory);
            }

            Store.BeforeDataChanged += SaveData;

            _logger.LogInformation("Starting notification publisher");


            _logger.LogInformation("Starting data server");
            // this is the server responsible for client requests and synchronization requests from other servers
            DataServer = new(Store, ClusterServer);

            var port = DataServer.Start(config.NetworkConfiguration.TcpClientPort);

            // in case the port is dynamic 
            ClusterServer.DataPort = port;

            _logger.LogInformation("Starting cluster coordinator");
            ClusterServer.Start();
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
            try
            {
                _logger.LogInformation(
                    $"Saving change for collection {e.ChangedData.Collection} key {e.ChangedData.Key}");
                _persistenceEngine.SaveChange(e.ChangedData, Config.DataDirectory);
            }
            catch (Exception exception)
            {
                _logger.LogError($"Error saving change:{exception.Message} ");
            }
    }
}