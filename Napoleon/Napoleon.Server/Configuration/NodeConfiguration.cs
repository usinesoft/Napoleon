namespace Napoleon.Server.Configuration;

/// <summary>
/// Basic configuration for a node in the cluster
/// </summary>
public class NodeConfiguration
{
    /// <summary>
    /// Cluster name is mandatory
    /// </summary>
    public string? ClusterName { get; set; }

    public NetworkConfiguration NetworkConfiguration { get; set; } = new();
}