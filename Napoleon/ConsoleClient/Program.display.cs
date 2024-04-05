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

        table.AddRow("collection.key", "display the value of a key in the collection", "config.env");

        
        AnsiConsole.WriteLine();

        AnsiConsole.Write(table);
    }
}