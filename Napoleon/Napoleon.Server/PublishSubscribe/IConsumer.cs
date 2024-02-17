namespace Napoleon.Server.PublishSubscribe;

public interface IConsumer
{
    public void Start(string clusterName, string nodeId);

    public event EventHandler<MessageReceivedEventArgs> MessageReceived;
}