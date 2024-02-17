using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe.UdpImplementation;

namespace Napoleon.Tests;

public class UdpTests
{
    [Test]
    public void Consumer_start_and_stop_no_message()
    {
        var config = ConfigurationHelper.CreateDefault("test");

        var consumer = new Consumer(config.NetworkConfiguration.BroadcastPort,
            config.NetworkConfiguration.BroadcastAddress!);

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

        using var consumer = new Consumer(config.NetworkConfiguration.BroadcastPort,
            config.NetworkConfiguration.BroadcastAddress!);

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

        using var consumer = new Consumer(config.NetworkConfiguration.BroadcastPort,
            config.NetworkConfiguration.BroadcastAddress!);

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
}