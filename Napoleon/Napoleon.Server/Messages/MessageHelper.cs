using Napoleon.Server.Configuration;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;
using System.Text.Json;
using System.Xml;

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

    /// <summary>
    /// Extract the mandatory requestType value from a json message
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static int RequestType(this JsonDocument request)
    {
        var typeProperty = request.RootElement.GetProperty(RequestConstants.PropertyNameRequestType);

        if (typeProperty.ValueKind != JsonValueKind.Number)
            throw new ArgumentException($"{RequestConstants.PropertyNameRequestType} property is not a number",
                nameof(request));

        // request
        var requestType = typeProperty.GetInt32();
        return requestType;
    }

    
    public static bool GetBool(this JsonDocument @this, string propertyName, bool? defaultValue = null ) 
    {
        if (@this.RootElement.TryGetProperty(propertyName, out var value))
        {
            return value.GetBoolean();
        }

        if (defaultValue.HasValue)
        {
            return defaultValue.Value;
        }

        throw new ArgumentException($"Required property {propertyName} not found");
    }

    public static int GetInt(this JsonDocument @this, string propertyName, int? defaultValue = null ) 
    {
        if (@this.RootElement.TryGetProperty(propertyName, out var value))
        {
            return value.GetInt32();
        }

        if (defaultValue.HasValue)
        {
            return defaultValue.Value;
        }

        throw new ArgumentException($"Required property {propertyName} not found");
    }

    public static string GetString(this JsonDocument @this, string propertyName) 
    {
        if (@this.RootElement.TryGetProperty(propertyName, out var value))
        {
            return value.GetString() ?? throw new ArgumentException($"Required property {propertyName} not found");
        }

        throw new ArgumentException($"Required property {propertyName} not found");
    }

    public static JsonElement GetValue(this JsonDocument @this, string propertyName) 
    {
        if (@this.RootElement.TryGetProperty(propertyName, out var value))
        {
            return value;
        }

        throw new ArgumentException($"Required property {propertyName} not found");
    }
}