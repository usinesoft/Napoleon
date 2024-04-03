using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Napoleon.Client;
using Napoleon.Server;
using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe.UdpImplementation;
using Napoleon.Server.SharedData;

namespace Napoleon.Tests;

public class DataSynchronizationWithClient
{
    private List<ServerSuite> StartServersWithTcp(string cluster, params int[] ports)
    {
        var nodes = new List<ServerSuite>();

        var persistenceMock = new Mock<IPersistenceEngine>();

        Parallel.ForEach(ports, port =>
        {
            var config = ConfigurationHelper.CreateDefault(cluster);
            config.HeartbeatPeriodInMilliseconds = 100;
            config.NetworkConfiguration.TcpClientPort = port;
            config.NodeIdPolicy = NodeIdPolicy.ImplicitIpAndPort;

            var store = new DataStore();

            var coordinator = new ClusterCoordinator(
                new Publisher(config.NetworkConfiguration.MulticastAddress!, config.NetworkConfiguration.MulticastPort),
                new Consumer(config.NetworkConfiguration.MulticastAddress!, config.NetworkConfiguration.MulticastPort),
                store, config, new NullLogger<ClusterCoordinator>(), persistenceMock.Object
            );

            var serverSuite = new ServerSuite(new NullLogger<ServerSuite>(), coordinator, persistenceMock.Object, store);

            serverSuite.Start(config);

            lock (serverSuite)
            {
                nodes.Add(serverSuite);
            }
        });

        return nodes;
    }

    private static async Task WaitForLeaderElection(ICollection<ServerSuite> servers)
    {
        for (var i = 0; i < 10; i++)
        {
            var leaders = servers.Count(x => x.ClusterServer.MyStatus == StatusInCluster.Leader);
            var followers = servers.Count(x => x.ClusterServer.MyStatus == StatusInCluster.Follower);
            if (leaders == 1 && leaders + followers == servers.Count) return;

            await Task.Delay(500);
        }

        Assert.Fail("Leader election was not successful");
    }


    [Test]
    public async Task Client_connects_to_cluster_and_gets_status()
    {
        var servers = StartServersWithTcp("CL01", 0, 0, 0).ToList();

        await WaitForLeaderElection(servers);

        var follower1 = servers.First(x => x.ClusterServer.MyStatus == StatusInCluster.Follower);
        Assert.IsNotNull(follower1);

        ClusterClient client = new();
        await client.Connect(follower1.ClusterServer.MyNodeId!);
        var clusterStatus = client.ClusterStatus;
        Assert.IsNotNull(clusterStatus);
        Assert.That(clusterStatus.Count, Is.EqualTo(3));


        // stop all
        client.Dispose();

        foreach (var server in servers) server.Dispose();

        await Task.Delay(100);
    }

    [Test]
    public async Task Client_updates_data_with_leader_connection()
    {
        var servers = StartServersWithTcp("CL02", 0, 0, 0).ToList();

        await WaitForLeaderElection(servers);


        var follower1 = servers.First(x => x.ClusterServer.MyStatus == StatusInCluster.Follower);

        ClusterClient client = new();

        await client.Connect(follower1.ClusterServer.MyNodeId!);

        var leaderClient = await client.GetLeaderDataClient();
        Assert.IsNotNull(leaderClient);

        await leaderClient.PutValue("stuff", "01", true);
        await leaderClient.PutValue("stuff", "02", 44);

        // wait for the leader to propagate changes
        await Task.Delay(70);
        var (val, found) = client.Data.TryGetScalarValue<bool>("stuff", "01");

        Assert.IsTrue(found);
        Assert.IsTrue(val);

        (var val1, found) = client.Data.TryGetScalarValue<int>("stuff", "02");

        Assert.IsTrue(found);
        Assert.That(val1, Is.EqualTo(44));

        // delete a value
        await leaderClient.DeleteValue("stuff", "02");
        // wait for the leader to propagate changes
        await Task.Delay(70);

        (_, found) = client.Data.TryGetScalarValue<int>("stuff", "02");

        Assert.IsFalse(found);

        // stop all
        client.Dispose();

        foreach (var server in servers) server.Dispose();

        await Task.Delay(100);
    }

    [Test]
    public async Task When_a_new_node_joins_the_cluster_it_synchronizes_its_data()
    {
        var servers = StartServersWithTcp("CL03", 0, 0, 0).ToList();

        await WaitForLeaderElection(servers);


        var follower1 = servers.First(x => x.ClusterServer.MyStatus == StatusInCluster.Follower);
        var leader = servers.Single(x => x.ClusterServer.MyStatus == StatusInCluster.Leader);

        ClusterClient client = new();

        await client.Connect(follower1.ClusterServer.MyNodeId!);

        var leaderClient = await client.GetLeaderDataClient();
        Assert.IsNotNull(leaderClient);

        await leaderClient.PutValue("stuff", "01", 100);
        await leaderClient.PutValue("stuff", "02", "very good");

        await Task.Delay(200);

        // check all the servers are synchronized
        Assert.That(leader.Store.GlobalVersion, Is.EqualTo(2));
        Assert.That(follower1.Store.GlobalVersion, Is.EqualTo(2));

        // start a new server
        var newServer = StartServersWithTcp("CL03", 0)[0];
        // give him the time to synchronize
        await Task.Delay(2000);
        Assert.That(newServer.Store.GlobalVersion, Is.EqualTo(2));
    }
}