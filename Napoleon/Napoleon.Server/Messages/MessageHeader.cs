namespace Napoleon.Server.Messages;

/// <summary>
///     The same header for all message types
/// </summary>
public class MessageHeader : IAsRawBytes
{
    /// <summary>
    ///     As unique as possible for a reasonable size. Used to avoid duplicated messages in most cases.
    /// </summary>
    public int MessageId { get; set; }

    public string? Cluster { get; set; }

    /// <summary>
    ///     Unique identifier inside a cluster
    /// </summary>
    public string? SenderNode { get; set; }

    public StatusInCluster SenderStatus { get; set; }
    
    public MessageType MessageType { get; set; }

    /// <summary>
    /// Ip address of the sender
    /// </summary>
    public string? SenderIp { get; set; }

    /// <summary>
    /// TCP port used by the clients to connect to the sender
    /// </summary>
    public int SenderPortForClients { get; set; }

    /// <summary>
    /// Period between sender heart-beats
    /// </summary>
    public  int HeartbeatPeriodInMilliseconds { get; set; }
    
    /// <summary>
    ///     Optional payload size (zero for heartbeat messages)
    /// </summary>
    public int PayloadSize => Payload.Length;

    public byte[] Payload { get; set; }= Array.Empty<byte>();

    public byte[] ToRawBytes()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        writer.Write(MessageId);
        writer.Write(Cluster ?? string.Empty);
        writer.Write(SenderNode ?? string.Empty);
        writer.Write((int)SenderStatus);
        writer.Write((int)MessageType);
        writer.Write(SenderIp??string.Empty);
        writer.Write(SenderPortForClients);
        writer.Write(HeartbeatPeriodInMilliseconds);

        writer.Write(PayloadSize);
        writer.Flush();

        return stream.ToArray();
    }

    public void FromRawBytes(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);

        MessageId = reader.ReadInt32();
        Cluster = reader.ReadString();
        SenderNode = reader.ReadString();
        SenderStatus = (StatusInCluster)reader.ReadInt32();
        MessageType = (MessageType)reader.ReadInt32();
        SenderIp = reader.ReadString();
        SenderPortForClients  = reader.ReadInt32();
        HeartbeatPeriodInMilliseconds =  reader.ReadInt32();

        var payloadSize = reader.ReadInt32();
        
        Payload = payloadSize > 0 ? reader.ReadBytes(PayloadSize) : Array.Empty<byte>();
    }

    public override string ToString()
    {
        return
            $"{nameof(MessageId)}: {MessageId}, {nameof(Cluster)}: {Cluster}, {nameof(SenderNode)}: {SenderNode}, {nameof(MessageType)}: {MessageType}, {nameof(PayloadSize)}: {PayloadSize}";
    }
}