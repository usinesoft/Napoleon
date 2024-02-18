using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe.UdpImplementation;

namespace Napoleon
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var config = ConfigurationHelper.CreateDefault("DEV");

            using var publisher = new Publisher(config.NetworkConfiguration.BroadcastAddress!,
                config.NetworkConfiguration.BroadcastPort);

            using var consumer = new Consumer(config.NetworkConfiguration.BroadcastAddress!,
                config.NetworkConfiguration.BroadcastPort);


            using var server = new Server.Server.Server(publisher, consumer);

            server.Run(config.ClusterName!);

            bool shouldStop = false;
            Console.CancelKeyPress += (sender, cancelEventArgs)=>
            {
                shouldStop = true;
            };

            while (!shouldStop)
            {
                Console.Title = $"{server.MyStatus} in cluster {config.ClusterName} ({server.NodesAliveInCluster} nodes alive)";

                await Task.Delay(100);
            }

            Console.WriteLine("Stopping");

            
        }
    }
}
