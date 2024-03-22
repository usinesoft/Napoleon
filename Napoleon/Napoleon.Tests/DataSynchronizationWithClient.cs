using Napoleon.Client;
using Napoleon.Server;
using Napoleon.Server.Configuration;

namespace Napoleon.Tests;

public class DataSynchronizationWithClient
{
    IEnumerable<ServerSuite> StartServersWithTcp(params int[] ports)
    {
        foreach (var port in ports)
        {
            var config = ConfigurationHelper.CreateDefault("UT1");
            config.NetworkConfiguration.TcpClientPort = port;
            config.NodeIdPolicy = NodeIdPolicy.ImplicitIpAndPort;

            var serverSuite = new ServerSuite();
            serverSuite.Start(config);

            yield return serverSuite;
        }
    }

    [Test]
    public async Task Client_connects_to_cluster_and_gets_status()
    {
        var servers = StartServersWithTcp(0, 0, 0).ToList();

        await Task.Delay(2000);

        Assert.That(servers.Count, Is.EqualTo(3));

        var leaders = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Leader);
        var followers = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);
        
        Assert.That(leaders, Is.EqualTo(1));
        Assert.That(followers, Is.EqualTo(2));

        var follower1 = servers.First(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);
        Assert.IsNotNull(follower1);

        ClusterClient client = new();
        await client.Connect(follower1.ClusterServer!.MyNodeId!);
        var clusterStatus = client.ClusterStatus;
        Assert.IsNotNull(clusterStatus);
        Assert.That(clusterStatus.Count, Is.EqualTo(3));


        
        // stop all
        client.Dispose();

        foreach (var server in servers)
        {
            server.Dispose();
        }

        await Task.Delay(100);

    }

    [Test]
    public async Task Client_updates_data_with_leader_connection()
    {
        var servers = StartServersWithTcp(0, 0, 0).ToList();

        await Task.Delay(2000);

        Assert.That(servers.Count, Is.EqualTo(3));

        var leaders = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Leader);
        var followers = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);
        
        Assert.That(leaders, Is.EqualTo(1));
        Assert.That(followers, Is.EqualTo(2));

        var follower1 = servers.First(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);

        ClusterClient client = new();

        await client.Connect(follower1.ClusterServer!.MyNodeId!);

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

        // stop all
        client.Dispose();

        foreach (var server in servers)
        {
            server.Dispose();
        }

        await Task.Delay(100);

    }

    [Test]
    public async Task When_a_new_node_joins_the_cluster_it_synchronizes_its_data()
    {
        var servers = StartServersWithTcp(0, 0, 0).ToList();

        await Task.Delay(2000);

        Assert.That(servers.Count, Is.EqualTo(3));

        var leaders = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Leader);
        var followers = servers.Count(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);
        
        Assert.That(leaders, Is.EqualTo(1));
        Assert.That(followers, Is.EqualTo(2));

        var follower1 = servers.First(x=>x.ClusterServer!.MyStatus == StatusInCluster.Follower);
        var leader = servers.Single(x=>x.ClusterServer!.MyStatus == StatusInCluster.Leader);

        ClusterClient client = new();

        await client.Connect(follower1.ClusterServer!.MyNodeId!);

        var leaderClient = await client.GetLeaderDataClient();
        Assert.IsNotNull(leaderClient);

        await leaderClient.PutValue("stuff", "01", 100);
        await leaderClient.PutValue("stuff", "02", "very good");

        await Task.Delay(200);

        // check all the servers are synchronized
        Assert.That(leader.Store.GlobalVersion, Is.EqualTo(2));
        Assert.That(follower1.Store.GlobalVersion, Is.EqualTo(2));

        // start a new server
        var newServer = StartServersWithTcp(0).First();
        // give him the time to synchronize
        await Task.Delay(1200);
        Assert.That(newServer.Store.GlobalVersion, Is.EqualTo(2));

    }
}