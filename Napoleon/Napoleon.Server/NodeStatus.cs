using Napoleon.Server.Configuration;

namespace Napoleon.Server;

public class NodeStatus
{
    public NodeStatus(StatusInCluster statusInCluster, int heartbeatPeriodInMilliseconds, int tcpClientPort, string tcpAddress, string?nodeId = null)
    {
        ExplicitNodeId = nodeId;
        LastHeartbeat = DateTime.Now;
        StatusInCluster = statusInCluster;
        HeartbeatPeriodInMilliseconds = heartbeatPeriodInMilliseconds;
        TcpClientPort = tcpClientPort;
        TcpAddress = tcpAddress;
    }

    /// <summary>
    /// Optional explicit node id
    /// </summary>
    private string? ExplicitNodeId { get; }

    /// <summary>
    /// Either explicit or IpAddress + IpPort which is guaranteed to be unique
    /// </summary>
    public string NodeId => ExplicitNodeId ?? $"{TcpAddress}:{TcpClientPort}";

    public StatusInCluster StatusInCluster { get; }

    private DateTimeOffset LastHeartbeat { get; }

    public int HeartbeatPeriodInMilliseconds { get; }

    public int TcpClientPort { get; }
    
    public string TcpAddress { get; }

    
    public bool IsAlive => (DateTimeOffset.Now - LastHeartbeat).TotalMilliseconds <
                           HeartbeatPeriodInMilliseconds * Constants.HearbeatsLostBeforeDeath;
}