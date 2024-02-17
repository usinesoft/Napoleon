namespace Napoleon.Server.Configuration;

public class NetworkConfiguration
{
    /// <summary>
    /// Group address for UDP multicast
    /// </summary>
    public string? BroadcastAddress { get; set; }

    /// <summary>
    /// Port for UDP multicast
    /// </summary>
    public int BroadcastPort { get; set; }

    /// <summary>
    /// My own optional TCP port (0 means dynamically allocate)
    /// </summary>
    public int? MyTcpPort { get; set; }

    /// <summary>
    /// Nodes in the same cluster that can be contacted only by TCP
    /// </summary>
    public IList<PartnerConfiguration> PartnerConfigurations { get; set; } = new List<PartnerConfiguration>();

}