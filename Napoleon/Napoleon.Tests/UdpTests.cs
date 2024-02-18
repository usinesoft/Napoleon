using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe.UdpImplementation;
using Napoleon.Server.Server;
using static Napoleon.Server.Server.Server;

namespace Napoleon.Tests;

public class UdpTests
{
    [Test]
    public void Consumer_start_and_stop_no_message()
    {
        var config = ConfigurationHelper.CreateDefault("test");

        var consumer = new Consumer(config.NetworkConfiguration.BroadcastAddress!, config.NetworkConfiguration.BroadcastPort);

        consumer.MessageReceived += (sender, args) => { Assert.Fail("No message should have been received"); };

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

        var producer = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        Assert.DoesNotThrow(() => { producer.Publish(MessageHelper.CreateHeartbeat("test", "node01")); });


        producer.Dispose();
    }


    /// <summary>
    /// Messages should be received only if they come from a different node of the same cluster
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
    public async Task Publish_and_consume(string publisherCluster, string publisherNode, string consumerCluster, string consumerNode, bool shouldHaveReceivedMessage)
    {
        var config = ConfigurationHelper.CreateDefault(consumerCluster);

        using var consumer = new Consumer(config.NetworkConfiguration.BroadcastAddress!, config.NetworkConfiguration.BroadcastPort);

        consumer.Start(config.ClusterName!, consumerNode);

        var messageReceived = false;

        consumer.MessageReceived += (sender, args) =>
        {
            Console.WriteLine(args.MessageHeader);

            messageReceived = true;
        };

        using var producer = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        producer.Publish(MessageHelper.CreateHeartbeat(publisherCluster, publisherNode));

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(shouldHaveReceivedMessage));
    }

    [Test]
    public async Task Duplicate_messages_are_ignored()
    {
        var config = ConfigurationHelper.CreateDefault("c1");

        using var consumer = new Consumer(config.NetworkConfiguration.BroadcastAddress!, config.NetworkConfiguration.BroadcastPort);

        consumer.Start(config.ClusterName!, "node1");

        var messageReceived = 0;

        consumer.MessageReceived += (sender, args) =>
        {
            Console.WriteLine(args.MessageHeader);

            messageReceived++;
        };

        using var producer = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        producer.Publish(new MessageHeader
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 13
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(1));

        // publish the same message again
        producer.Publish(new MessageHeader
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 13
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(1), "the duplicate message should have been ignored");

        // change the message id (this will be considered a new message)
        producer.Publish(new MessageHeader
        {
            SenderNode = "node2",
            Cluster = config.ClusterName,
            MessageType = MessageType.Heartbeat,
            MessageId = 14
        });

        await Task.Delay(100);

        Assert.That(messageReceived, Is.EqualTo(2));
        
    }

    
    [Test]
    public async Task Leader_election()
    {
        var config = ConfigurationHelper.CreateDefault("cx2");


        // start the first server
        var consumer1 = new Consumer(config.NetworkConfiguration.BroadcastAddress!, config.NetworkConfiguration.BroadcastPort);
        
        var producer1 = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        
        using var server1 = new Server.Server.Server(producer1, consumer1);
        server1.Run(config.ClusterName!);

        await Task.Delay(NodeStatus.HeartBeatFrequencyInMilliseconds + 100);

        Assert.That(server1.MyStatus, Is.EqualTo(StatusInCluster.HomeAlone));
        Assert.That(server1.NodesAliveInCluster, Is.EqualTo(1));

        // start the second server
        var consumer2 = new Consumer(config.NetworkConfiguration.BroadcastAddress!, config.NetworkConfiguration.BroadcastPort);
        
        var producer2 = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        using var server2 = new Server.Server.Server(producer2, consumer2);
        server2.Run(config.ClusterName!);

        await Task.Delay(NodeStatus.HeartBeatFrequencyInMilliseconds + 100);
        
        Assert.That(server1.NodesAliveInCluster, Is.EqualTo(2));
        Assert.That(server2.NodesAliveInCluster, Is.EqualTo(2));

        var status = new[] { server1.MyStatus, server2.MyStatus };

        Assert.That(status.Count(x=>x == StatusInCluster.Follower), Is.EqualTo(1));
        Assert.That(status.Count(x=>x == StatusInCluster.Leader), Is.EqualTo(1));

        // now kill the leader
        var leader = server1.MyStatus == StatusInCluster.Leader ? server1 : server2;
        var follower = server1.MyStatus == StatusInCluster.Follower ? server1 : server2;

        leader.Dispose();

        await Task.Delay(NodeStatus.MillisecondsBeforeDead + NodeStatus.HeartBeatFrequencyInMilliseconds + 100);

        Assert.That(follower.MyStatus, Is.EqualTo(StatusInCluster.HomeAlone), "a single node can not be follower");

    }
}