using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Napoleon.Server.Configuration;

public static class ConfigurationHelper
{
    public static NodeConfiguration CreateDefault(string clusterName)
    {
        return new()
        {
            ClusterName = clusterName,
            HeartbeatPeriodInMilliseconds = 500,
            NodeIdPolicy = NodeIdPolicy.Guid,
            
            NetworkConfiguration = new()
            {
                MulticastPort = Constants.DefaultMulticastPort,
                MulticastAddress = Constants.DefaultMulticastAddress,
            }
        };
    }

    public static NodeConfiguration? TryLoadFromFile(string configFilePath)
    {
        if (!File.Exists(configFilePath)) { return null; }

        using var stream = File.OpenRead(configFilePath);
        return JsonSerializer.Deserialize<NodeConfiguration>(stream);
    }


    /// <summary>
    /// Throw an exception if invalid configuration
    /// </summary>
    /// <param name="networkConfig"></param>
    private static void CheckNetworkConfiguration(this NetworkConfiguration networkConfig)
    {
         
        if (networkConfig.ServerToServerProtocol == NotificationProtocol.Tcp)
        {
            if(networkConfig.ServerLists.Length == 0) throw new ArgumentException($"The server to server protocol is TCP and no server address is specified");

            foreach (var hostAndPort in networkConfig.ServerLists)
            {
                if (string.IsNullOrWhiteSpace(hostAndPort)) throw new ArgumentException($"Invalid entry in server list {hostAndPort}");

                var parts = hostAndPort.Split(':',StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length != 2) throw new ArgumentException($"Invalid entry in server list {hostAndPort}");
            
                var host = parts[0];
                var port = parts[1];
                if(!int.TryParse(port, out var tcpPort)) throw new ArgumentException($"Invalid entry in server list {hostAndPort}");

                var hostType = Uri.CheckHostName(host);

                if (hostType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)) throw new ArgumentException($"Invalid entry in server list {hostAndPort}");

                if (tcpPort is <= IPEndPoint.MinPort or >= IPEndPoint.MaxPort) throw new ArgumentException($"Invalid entry in server list {hostAndPort}. The port number is not valid");

            }
        }


        if (networkConfig.ServerToServerProtocol == NotificationProtocol.UdpMulticast)
        {
            var validMulticastAddress = false;

            // check the multicast address
            if (!string.IsNullOrWhiteSpace(networkConfig.MulticastAddress))
            {
                var hostType = Uri.CheckHostName(networkConfig.MulticastAddress);
                if (hostType == UriHostNameType.IPv4 &&
                    IPAddress.TryParse(networkConfig.MulticastAddress, out var address) &&
                    address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = address.GetAddressBytes();
                    if (bytes.Length == 4 && bytes[0] is >= 224 and <= 239) validMulticastAddress = true;
                }
            }

            if (!validMulticastAddress)
                throw new ArgumentException($"Invalid multicast address:{networkConfig.MulticastAddress}");

            if (networkConfig.MulticastPort is <= IPEndPoint.MinPort or >= IPEndPoint.MaxPort)
                throw new ArgumentException($"Invalid multicast port:{networkConfig.MulticastPort}");

        }


    }


    /// <summary>
    ///     Throw an exception if configuration is invalid
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static void CheckConfiguration(this NodeConfiguration config)
    {
        // cluster name is mandatory
        if (string.IsNullOrWhiteSpace(config.ClusterName)) throw new ArgumentException("Invalid cluster name");

        // it should contain multicast configuration or at least a partner address (both can be available)
        config.NetworkConfiguration.CheckNetworkConfiguration();
    }
}