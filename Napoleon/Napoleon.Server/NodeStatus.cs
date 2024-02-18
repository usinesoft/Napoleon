using Napoleon.Server.Configuration;

namespace Napoleon.Server;

public class NodeStatus
{
    public NodeStatus(string nodeId, StatusInCluster statusInCluster)
    {
        NodeId = nodeId;
        LastHeartbeat = DateTime.Now;
        StatusInCluster = statusInCluster;
    }

    public string NodeId { get; }

    public StatusInCluster StatusInCluster { get; }

    private DateTimeOffset LastHeartbeat { get; }

    public bool IsAlive => (DateTimeOffset.Now - LastHeartbeat).TotalMilliseconds <
                           NodeConfiguration.TimeBeforeDeathInMilliseconds;
}