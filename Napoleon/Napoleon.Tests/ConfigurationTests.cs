using Napoleon.Server.Configuration;
using System.Text.Json;

namespace Napoleon.Tests;

public class ConfigurationTests
{
    [Test]
    public void Default_configuration_is_valid()
    {
        var cfg = ConfigurationHelper.CreateDefault("test");

        Assert.DoesNotThrow(cfg.CheckConfiguration, "the default configuration is not valid");
    }

    [Test]
    public void Configuration_with_only_cluster_name_is_not_valid()
    {
        var empty = new NodeConfiguration();

        Assert.Throws<ArgumentException>(()=>empty.CheckConfiguration(), "empty configuration should not be valid");

        var withCluster = new NodeConfiguration
        {
            ClusterName = "my cluster"
        };

        Assert.Throws<ArgumentException>(()=>withCluster.CheckConfiguration(), "empty configuration should not be valid");

    }

    [Test]
    public void Check_config_with_multicast_group()
    {
        var invalidGroupAddress = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { MulticastAddress = "220.0.0.12", MulticastPort = 8888 }
        };

        Assert.Throws<ArgumentException>(()=>invalidGroupAddress.CheckConfiguration(),
            "configuration with invalid group address should not be valid");

        var invalidGroupPort = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { MulticastAddress = "225.0.0.12", MulticastPort = 0 }
        };

        Assert.Throws<ArgumentException>(()=>invalidGroupPort.CheckConfiguration(),
            "configuration with invalid group address should not be valid");

        var validMulticastConfig = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { MulticastAddress = "225.0.0.12", MulticastPort = 7878 }
        };

        Assert.DoesNotThrow(validMulticastConfig.CheckConfiguration, 
            "configuration with valid multicast group should be valid");
    }

    [Test]
    public void Configuration_is_serializable()
    {
        var cfg = new NodeConfiguration
        {
            ClusterName = "PROD", 
            HeartbeatPeriodInMilliseconds = 500,
            NodeIdPolicy = NodeIdPolicy.ImplicitIpAndPort,
            NetworkConfiguration = new()
            {
                MulticastPort = 50501,
                MulticastAddress = "224.101.102.103",
                TcpClientPort = 50601,
                ServerToServerProtocol = NotificationProtocol.UdpMulticast
            }
        };

        var json = cfg.AsJson();

        Assert.False(string.IsNullOrWhiteSpace(json));
    }
}