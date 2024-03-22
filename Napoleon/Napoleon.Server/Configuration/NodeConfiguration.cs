using System;
using System.Text.Json.Serialization;

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


    /// <summary>
    /// Nodes may have different heart-beat periods
    /// </summary>
    public int HeartbeatPeriodInMilliseconds { get; set; }

    /// <summary>
    /// How to generate an unique node id
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NodeIdPolicy>))]
    public NodeIdPolicy NodeIdPolicy { get; set; }

    /// <summary>
    /// Explicit node id. Mandatory if <see cref="NodeIdPolicy"/> is ExplicitName.
    /// Not required otherwise (ignored if filled)
    /// </summary>
    public string? NodeId { get; set; }

 
    /// <summary>
    /// Network configuration for clients and server-server communication
    /// </summary>
    public NetworkConfiguration NetworkConfiguration { get; set; } = new();
}