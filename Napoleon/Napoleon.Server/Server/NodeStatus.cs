namespace Napoleon.Server.Server;

public class NodeStatus
{
    public const int HeartBeatFrequencyInMilliseconds = 1000;

    public const int MillisecondsBeforeDead = HeartBeatFrequencyInMilliseconds * 3;

    public NodeStatus(string nodeId)
    {
        NodeId = nodeId;
        LastHeartbeat = DateTime.Now;
    }

    public string NodeId { get; set; }
    public DateTimeOffset LastHeartbeat { get; }

    public bool IsAlive => (DateTimeOffset.Now  - LastHeartbeat).TotalMilliseconds < MillisecondsBeforeDead;
}