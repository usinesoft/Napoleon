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
    public DataStore Store { get; } = new DataStore();

    public Server? ClusterServer { get; private set; }

    public DataServer? DataServer { get; private set; }


    public void Start(NodeConfiguration config)
    {
        
        var publisher = new Publisher(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        // this is the server responsible for cluster status (including data synchronization) and leader election
        ClusterServer = new Server(publisher, consumer, Store, config);

        // this is the server responsible for client requests and synchronization requests from other servers
        DataServer = new DataServer(Store, ClusterServer);

        int port = DataServer.Start(config.NetworkConfiguration.TcpClientPort);

        // in case the port is dynamic 
        ClusterServer.DataPort = port;
        ClusterServer.Run();
    }

    public void Dispose()
    {
        ClusterServer?.Dispose();
        DataServer?.Dispose();
    }
}