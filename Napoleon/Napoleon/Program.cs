using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe.UdpImplementation;

namespace Napoleon;

internal static class Program
{
    private static async Task Main()
    {
        var config = ConfigurationHelper.CreateDefault("DEV");

        using var publisher = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);

        using var consumer = new Consumer(config.NetworkConfiguration.BroadcastAddress!,
            config.NetworkConfiguration.BroadcastPort);


        using var server = new Server.Server(publisher, consumer);

        server.Run(config.ClusterName!);

        var shouldStop = false;
        Console.CancelKeyPress += (_, _) => { shouldStop = true; };

        while (!shouldStop)
        {
            Console.Title =
                $"{server.MyStatus} in cluster {config.ClusterName} ( {server.NodesAliveInCluster} nodes alive)";

            await Task.Delay(100);
        }

        Console.WriteLine("Stopping");
    }
}