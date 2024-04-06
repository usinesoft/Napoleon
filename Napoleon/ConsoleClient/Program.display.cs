using System.Text.Json.Nodes;
using Napoleon.Client;
using Napoleon.Server;
using Spectre.Console;

namespace ConsoleClient;

internal static partial class Program
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

            table.AddRow(server.NodeId, status, server.TcpAddress, server.TcpClientPort.ToString(),
                server.DataVersion.ToString(), strAlive);
        }

        AnsiConsole.WriteLine();

        AnsiConsole.Write(table);
    }

    private static void DisplayHelp()
    {
        var table = new Table();

        table.Title("[deepskyblue1]Available commands[/]");
        table.AddColumn("command");
        table.AddColumn("description");
        table.AddColumn("examples");

        string valuesExample = """value can be: true, false, "hello", 3.14, {"x":15.5, "y":14}, null """;

        table.AddRow("collection.key", "display the value of a key in the collection", "config.env");
        table.AddRow("collection.key=value", "add or update a value", valuesExample);
        table.AddRow("delete collection.key", "delete a value", "delete config.database_settings");

        
        AnsiConsole.WriteLine();

        AnsiConsole.Write(table);
    }

    private static async Task ProcessCommand(string command, ClusterClient clusterClient)
    {
        command = command.Trim().ToLower();

        var success = false;

        if (command.Contains('='))
        {
            var parts = command.Split('=', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var kv = parts[0].Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length == 2)
                {
                    var value = parts[1];
                    var collection = kv[0];
                    var key = kv[1];

                    var writeClient = await clusterClient.GetLeaderDataClient();

                    try
                    {
                        var jn = JsonNode.Parse(value);
                        await writeClient.PutValue(collection, key, jn);
                        Console.WriteLine("done");
                        success = true;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"{value} is not a valid json value");
                    }
                }
            }
        }
        else if (command.StartsWith("delete"))
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var kv = parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length == 2)
                {
                    var collection = kv[0];
                    var key = kv[1];

                    var writeClient = await clusterClient.GetLeaderDataClient();
                    await writeClient.DeleteValue(collection, key, CancellationToken.None);
                    Console.WriteLine("done");
                    success = true;
                }
            }
        }
        else if (command.Contains(".")) //get value
        {
            var kv = command.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (kv.Length == 2)
            {
                var collection = kv[0];
                var key = kv[1];

                var jsonValue = clusterClient.Data.TryGetValue(collection, key);
                Console.WriteLine(jsonValue.ToString());
                success = true;
            }
        }

        if (!success) Console.WriteLine("Invalid command");
    }
}