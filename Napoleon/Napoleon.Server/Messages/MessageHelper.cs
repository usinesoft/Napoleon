namespace Napoleon.Server.Messages;

public static class MessageHelper
{
    public static MessageHeader CreateHeartbeat(string cluster, string node)
    {
        return new()
        {
            Cluster = cluster, SenderNode = node, MessageType = MessageType.Heartbeat,
            MessageId = Guid.NewGuid().GetHashCode(), PayloadSize = 0
        };
    }

    public static bool IsValidHeartbeat(this MessageHeader message)
    {
        if (message.MessageType != MessageType.Heartbeat) return false;

        if (message.PayloadSize != 0) return false;

        if (string.IsNullOrWhiteSpace(message.SenderNode)) return false;

        if (string.IsNullOrWhiteSpace(message.Cluster)) return false;

        if (message.MessageId == 0) return false;

        return true;
    }

    public static MessageHeader Clone(this MessageHeader message)
    {
        var bytes = message.ToRawBytes();
        var header = new MessageHeader();
        header.FromRawBytes(bytes);

        return header;
    }
}