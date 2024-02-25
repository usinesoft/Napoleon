using Napoleon.Server.Configuration;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.Messages;

public static class MessageHelper
{
    public static MessageHeader CreateHeartbeat(NodeConfiguration config, string nodeId, StatusInCluster status, string myIpAddress)
    {
        return new()
        {
            Cluster = config.ClusterName, SenderNode = nodeId, MessageType = MessageType.Heartbeat,
            MessageId = Guid.NewGuid().GetHashCode(), HeartbeatPeriodInMilliseconds = config. HeartbeatPeriodInMilliseconds,
            SenderIp = myIpAddress,SenderPortForClients = config.NetworkConfiguration.TcpClientPort, SenderStatus = status
            
        };
    }

    public static MessageHeader CreateDataSyncMessage(string cluster, string node, IList<Item> items)
    {
        var payload = new DataSyncPayload{Items =  items };
        var bytes = payload.ToRawBytes();
        

        return new()
        {
            Cluster = cluster, SenderNode = node, MessageType = MessageType.DataSync,
            MessageId = Guid.NewGuid().GetHashCode(),
            Payload = bytes
        };
    }

    public static DataSyncPayload FromMessage(this MessageHeader message)
    {
        if (message.MessageType == MessageType.DataSync)
        {
            var payload = new DataSyncPayload();
            payload.FromRawBytes(message.Payload);
            return payload;
        }

        throw new ArgumentException($"Not a data sync message (type={message.MessageType})");
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