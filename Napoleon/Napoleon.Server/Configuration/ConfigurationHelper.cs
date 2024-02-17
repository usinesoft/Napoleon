using System.Net;
using System.Net.Sockets;

namespace Napoleon.Server.Configuration;

public static class ConfigurationHelper
{
    public static NodeConfiguration CreateDefault(string clusterName)
    {
        return new()
        {
            ClusterName = clusterName,
            NetworkConfiguration = new()
            {
                BroadcastPort = 50501,
                BroadcastAddress = "224.101.102.103"
            }
        };
    }


    public static bool CheckNetworkConfiguration(this NetworkConfiguration networkConfig)
    {
        var atLeastOneValidPartner = false;

        foreach (var configuration in networkConfig.PartnerConfigurations)
        {
            if (string.IsNullOrWhiteSpace(configuration.Host)) continue;

            var hostType = Uri.CheckHostName(configuration.Host);

            if (hostType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)) continue;

            if (configuration.TcpPort is <= IPEndPoint.MinPort or >= IPEndPoint.MaxPort) continue;

            atLeastOneValidPartner = true;
            break;
        }

        var validMulticastAddress = false;

        // check the multicast address
        if (!string.IsNullOrWhiteSpace(networkConfig.BroadcastAddress))
        {
            var hostType = Uri.CheckHostName(networkConfig.BroadcastAddress);
            if (hostType == UriHostNameType.IPv4 &&
                IPAddress.TryParse(networkConfig.BroadcastAddress, out var address) &&
                address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                if (bytes.Length == 4 && bytes[0] is >= 224 and <= 239) validMulticastAddress = true;
            }
        }

        var validMulticastGroup = validMulticastAddress &&
                                  networkConfig.BroadcastPort is > IPEndPoint.MinPort and < IPEndPoint.MaxPort;

        return validMulticastGroup || atLeastOneValidPartner;
    }


    /// <summary>
    ///     Check if a configuration is valid
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static bool CheckConfiguration(this NodeConfiguration config)
    {
        // cluster name is mandatory
        if (string.IsNullOrWhiteSpace(config.ClusterName)) return false;

        // it should contain multicast configuration or at least a partner address (both can be available)
        return config.NetworkConfiguration.CheckNetworkConfiguration();
    }
}