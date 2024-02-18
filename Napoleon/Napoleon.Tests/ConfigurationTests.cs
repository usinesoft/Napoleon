using Napoleon.Server.Configuration;

namespace Napoleon.Tests;

public class ConfigurationTests
{
    [Test]
    public void Default_configuration_is_valid()
    {
        var cfg = ConfigurationHelper.CreateDefault("test");

        Assert.That(cfg.CheckConfiguration, Is.True, "the default configuration is not valid");
    }

    [Test]
    public void Configuration_with_only_cluster_name_is_not_valid()
    {
        var empty = new NodeConfiguration();

        Assert.That(empty.CheckConfiguration, Is.False, "empty configuration should not be valid");

        var withCluster = new NodeConfiguration
        {
            ClusterName = "my cluster"
        };

        Assert.That(withCluster.CheckConfiguration, Is.False,
            "configuration without network information should not be valid");
    }

    [Test]
    public void Check_config_with_multicast_group()
    {
        var invalidGroupAddress = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { BroadcastAddress = "220.0.0.12", BroadcastPort = 8888 }
        };

        Assert.That(invalidGroupAddress.CheckConfiguration, Is.False,
            "configuration with invalid group address should not be valid");

        var invalidGroupPort = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { BroadcastAddress = "225.0.0.12", BroadcastPort = 0 }
        };

        Assert.That(invalidGroupPort.CheckConfiguration, Is.False,
            "configuration with invalid group port should not be valid");

        var validMulticastConfig = new NodeConfiguration
        {
            ClusterName = "01",
            NetworkConfiguration = { BroadcastAddress = "225.0.0.12", BroadcastPort = 7878 }
        };

        Assert.That(validMulticastConfig.CheckConfiguration, Is.True,
            "configuration with valid multicast group should be valid");
    }
}