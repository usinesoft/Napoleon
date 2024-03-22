using Napoleon.Server;
using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe.UdpImplementation;
using Napoleon.Server.SharedData;

namespace Napoleon.Tests;

public class UdpTests
{
    [Test]
    public void Consumer_start_and_stop_no_message()
    {
        var config = ConfigurationHelper.CreateDefault("test");

        var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        consumer.MessageReceived += (_, _) => { Assert.Fail("No message should have been received"); };

        Assert.DoesNotThrow(() =>
        {
            consumer.Start("test001", "node001");

            consumer.Dispose();
        });
    }

    [Test]
    public void Publish_without_consumer_waiting()
    {
        var config = ConfigurationHelper.CreateDefault("test");

        var producer = new Publisher(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        Assert.DoesNotThrow(() => { producer.Publish(MessageHelper.CreateHeartbeat(config, "node01", StatusInCluster.Follower, Server.Server.GetLocalIpAddress())); });


        producer.Dispose();
    }


    /// <summary>
    ///     Messages should be received only if they come from a different node of the same cluster
    /// </summary>
    /// <param name="publisherCluster"></param>
    /// <param name="publisherNode"></param>
    /// <param name="consumerCluster"></param>
    /// <param name="consumerNode"></param>
    /// <param name="shouldHaveReceivedMessage"></param>
    /// <returns></returns>
    [Test]
    [TestCase("cluster1", "node1", "cluster1", "node2", true)]
    [TestCase("cluster1", "node1", "cluster1", "node1", false)]
    [TestCase("cluster1", "node1", "cluster2", "node2", false)]
    public async Task Publish_and_consume(string publisherCluster, string publisherNode, string consumerCluster,
        string consumerNode, bool shouldHaveReceivedMessage)
    {
        var consumerConfig = ConfigurationHelper.CreateDefault(consumerCluster);

        using var consumer = new Consumer(consumerConfig.NetworkConfiguration.MulticastAddress!,
            consumerConfig.NetworkConfiguration.MulticastPort);

        consumer.Start(consumerConfig.ClusterName!, consumerNode);

        var messageReceived = false;

        consumer.MessageReceived += (_, args) =>
        {
            Console.WriteLine(args.MessageHeader);

            messageReceived = true;
        };

        var producerConfig = ConfigurationHelper.CreateDefault(publisherCluster);
        using var producer = new Publisher(producerConfig.NetworkConfiguration.MulticastAddress!,
            producerConfig.NetworkConfiguration.MulticastPort);

        producer.Publish(MessageHelper.CreateHeartbeat(producerConfig, publisherNode,StatusInCluster.Follower, Server.Server.GetLocalIpAddress()));

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(shouldHaveReceivedMessage));
    }

    [Test]
    public async Task Duplicate_messages_are_ignored()
    {
        var config = ConfigurationHelper.CreateDefault("c1");

        using var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        consumer.Start(config.ClusterName!, "node1");

        var messageReceived = 0;

        consumer.MessageReceived += (_, args) =>
        {
            Console.WriteLine(args.MessageHeader);

            messageReceived++;
        };

        using var producer = new Publisher(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        producer.Publish(new()
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 13
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(1));

        // publish the same message again
        producer.Publish(new()
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 13
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(1), "the duplicate message should have been ignored");

        // change the message id (this will be considered a new message)
        producer.Publish(new()
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 14
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(2));
    }


    Server.Server StartOneServer(NodeConfiguration config)
    {
        var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        var publisher = new Publisher(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        var dataStore = new DataStore();

        var server = new Server.Server(publisher, consumer, dataStore, config);
        server.Run();

        return server;
    }


    [Test]
    public async Task Leader_election()
    {
        var config = ConfigurationHelper.CreateDefault("cx2");
        
        // start the first server
        using var server1 = StartOneServer(config);
        
        await Task.Delay(config.HeartbeatPeriodInMilliseconds + 100);

        Assert.That(server1.MyStatus, Is.EqualTo(StatusInCluster.HomeAlone));
        Assert.That(server1.NodesAliveInCluster, Is.EqualTo(1));

        // start the second server
        using var server2 = StartOneServer(config);
        
        // wait for them to synchronize
        await Task.Delay(config.HeartbeatPeriodInMilliseconds + 100);

        Assert.That(server1.NodesAliveInCluster, Is.EqualTo(2));
        Assert.That(server2.NodesAliveInCluster, Is.EqualTo(2));

        var status = new[] { server1.MyStatus, server2.MyStatus };

        // one should be leader and the other follower
        Assert.That(status.Count(x => x == StatusInCluster.Follower), Is.EqualTo(1));
        Assert.That(status.Count(x => x == StatusInCluster.Leader), Is.EqualTo(1));

        // now kill the leader
        var leader = server1.MyStatus == StatusInCluster.Leader ? server1 : server2;
        var follower = server1.MyStatus == StatusInCluster.Follower ? server1 : server2;

        leader.Dispose();

        await Task.Delay((int)(config.HeartbeatPeriodInMilliseconds * (Constants.HearbeatsLostBeforeDeath + 1) + 100));

        Assert.That(follower.MyStatus, Is.EqualTo(StatusInCluster.HomeAlone), "a single node can not be follower");
    }
}