using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe
{
    public interface IPublisher
    {
        void Publish(MessageHeader message);
    }
}
