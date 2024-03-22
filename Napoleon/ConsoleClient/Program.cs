using Napoleon.Client;
using Napoleon.Server;
using Spectre.Console;

namespace ConsoleClient
{
    internal static class Program
    {
        private static void ServerStatusToConsole(IEnumerable<NodeStatus> nodes)
        {
            var table = new Table();

            table.Title("[deepskyblue1]Nodes in cluster[/]");
            table.AddColumn("node");
            table.AddColumn("status");
            table.AddColumn("IP");
            table.AddColumn("port");
            table.AddColumn("data-version");
            table.AddColumn("alive");

                
                
            foreach (var server in nodes)
            {
                var alive = server.IsAlive;
                
                var strAlive = alive ? "[green]alive[/]" : "[red]dead[/]";
                    
                var status = alive ? server.StatusInCluster.ToString() : " ";

                table.AddRow(server.NodeId, status, server.TcpAddress, server.TcpClientPort.ToString(), "54656", strAlive);
            }
            AnsiConsole.WriteLine();

            AnsiConsole.Write(table);
       
        }
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Interactive mode");
                

                var connected = false;

                ClusterClient clusterClient = new ClusterClient();

                while (!connected)
                {
                    
                    try
                    {
                        Console.WriteLine("connect to>");
                        var host = Console.ReadLine();
                        if(host == null) continue;
                        
                        await clusterClient.Connect(host);
                        
                        connected = true;


                    }
                    catch (Exception e)
                    {
                        Console.Write(e.ToString());
                        throw;
                    }

                }

                ServerStatusToConsole(clusterClient.ClusterStatus);
            }

        }
    }
}
