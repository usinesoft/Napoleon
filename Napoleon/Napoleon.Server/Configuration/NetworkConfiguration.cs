using System.Text.Json.Serialization;

namespace Napoleon.Server.Configuration;

public class NetworkConfiguration
{
    /// <summary>
    ///     Group address for UDP multicast
    /// </summary>
    public string? MulticastAddress { get; set; }

    /// <summary>
    ///     Port for UDP multicast
    /// </summary>
    public int MulticastPort { get; set; }

    /// <summary>
    ///     My own TCP port
    /// </summary>
    public int TcpClientPort { get; set; }

    /// <summary>
    ///     Either TCP or UDP multicast
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NotificationProtocol>))]
    public NotificationProtocol ServerToServerProtocol { get; set; }

    /// <summary>
    ///     Nodes in the same cluster. Must be filled if <see cref="ServerToServerProtocol" /> is TCP
    ///     They are specified as a list of host:port
    /// </summary>
    public string[] ServerLists { get; set; } = Array.Empty<string>();
}

public enum NotificationProtocol
{
    UdpMulticast,
    Tcp
}