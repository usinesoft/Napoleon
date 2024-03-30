using Napoleon.Server.Configuration;

namespace Napoleon.Server;

public class NodeStatus
{
    public NodeStatus(StatusInCluster statusInCluster, int heartbeatPeriodInMilliseconds, int tcpClientPort,
        string tcpAddress, string nodeId, int dataVersion)
    {
        NodeId = nodeId;
        LastHeartbeat = DateTime.Now;
        StatusInCluster = statusInCluster;
        HeartbeatPeriodInMilliseconds = heartbeatPeriodInMilliseconds;
        TcpClientPort = tcpClientPort;
        TcpAddress = tcpAddress;
        DataVersion = dataVersion;
    }


    /// <summary>
    ///     Either explicit or IpAddress + IpPort which is guaranteed to be unique
    /// </summary>
    public string NodeId { get; }

    public int DataVersion { get; }

    public StatusInCluster StatusInCluster { get; }

    private DateTimeOffset LastHeartbeat { get; }

    public int HeartbeatPeriodInMilliseconds { get; }

    public int TcpClientPort { get; }

    public string TcpAddress { get; }


    public bool IsAlive => (DateTimeOffset.Now - LastHeartbeat).TotalMilliseconds <
                           HeartbeatPeriodInMilliseconds * Constants.HearbeatsLostBeforeDeath;

    /// <summary>
    ///     In multicast mode if a node is dead for a long time forget it
    /// </summary>
    public bool IsForgotten => (DateTimeOffset.Now - LastHeartbeat).TotalMilliseconds >
                               HeartbeatPeriodInMilliseconds * Constants.HearbeatsLostBeforeDeath * 10;
}