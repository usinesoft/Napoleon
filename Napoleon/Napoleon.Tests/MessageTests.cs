using Napoleon.Server;
using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;

namespace Napoleon.Tests;

public class MessageTests
{
    [Test]
    public void Message_header_is_serializable()
    {
        var msg1 = new MessageHeader
        {
            MessageId = 123,
            Cluster = "test",
            MessageType = MessageType.Heartbeat,
            SenderNode = "node123"
        };

        var bytes1 = msg1.ToRawBytes();

        var msg2 = new MessageHeader();
        msg2.FromRawBytes(bytes1);

        Assert.That(msg2.ToString(), Is.EqualTo(msg1.ToString()));

        var bytes2 = msg2.ToRawBytes();

        CollectionAssert.AreEqual(bytes1, bytes2);
    }

    [Test]
    public void Generate_and_test_valid_heartbeat_messages()
    {
        var cfg = ConfigurationHelper.CreateDefault("test");
        var hb1 = MessageHelper.CreateHeartbeat(cfg, Guid.NewGuid().ToString(), StatusInCluster.Follower, Server.Server.GetLocalIpAddress());

        Assert.That(hb1.IsValidHeartbeat());

        var hb2 = hb1.Clone();

        Assert.That(hb2.IsValidHeartbeat());

        Assert.That(hb1.ToString(), Is.EqualTo(hb2.ToString()));
    }

    [Test]
    public async Task Test_semaphores()
    {
        SemaphoreSlim blocking = new(0);

        var task = Task.Run(async () =>
        {
            await blocking.WaitAsync();
        });

        await Task.Delay(100);

        Assert.That(task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));
        
        blocking.Release();

        await Task.Delay(100);

        Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        
    }

    
}