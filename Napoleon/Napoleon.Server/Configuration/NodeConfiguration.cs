namespace Napoleon.Server.Configuration;

/// <summary>
///     Basic configuration for a node in the cluster
/// </summary>
public class NodeConfiguration
{
    /// <summary>
    ///     Cluster name is mandatory
    /// </summary>
    public string? ClusterName { get; set; }


    public static int HeartbeatFrequencyInMilliseconds { get; set; } = 500;

    /// <summary>
    ///     Time before a node without heartbeat is declared dead
    /// </summary>
    public static int TimeBeforeDeathInMilliseconds { get; set; } = 1020;


    public NetworkConfiguration NetworkConfiguration { get; set; } = new();
}