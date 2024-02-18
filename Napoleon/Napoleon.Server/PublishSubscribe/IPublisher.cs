using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe
{
    public interface IPublisher:IDisposable
    {
        void Publish(MessageHeader message);
    }
}
