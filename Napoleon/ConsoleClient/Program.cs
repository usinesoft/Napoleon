using System.Text.Json.Nodes;
using Napoleon.Client;
using Napoleon.Server;
using Spectre.Console;

namespace ConsoleClient;

internal static partial class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Interactive mode");


            var connected = false;

            var clusterClient = new ClusterClient();

            clusterClient.ConnectionChanged += (obj, evt) => Console.Title = $"Connected to {evt.ConnectionInfo}";

            while (!connected)
                try
                {
                    Console.Write("connect to>");
                    var host = Console.ReadLine();
                    if (host == null) continue;

                    await clusterClient.Connect(host);

                    connected = true;

                    Console.WriteLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            ServerStatusToConsole(clusterClient.ClusterStatus);

            DisplayHelp();
            

            Console.Write(">");
            var command = Console.ReadLine();
            while (command != "exit")
            {
                if (command != null)
                    await ProcessCommand(command, clusterClient);
                Console.Write(">");
                command = Console.ReadLine();
            }
        }
    }
}