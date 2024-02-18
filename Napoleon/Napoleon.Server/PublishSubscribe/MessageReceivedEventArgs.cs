using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe;

public class MessageReceivedEventArgs : EventArgs
{
    public MessageReceivedEventArgs(MessageHeader messageHeader)
    {
        MessageHeader = messageHeader;
    }

    public MessageHeader MessageHeader { get; }
}