﻿using Microsoft.Extensions.DependencyInjection;
using Napoleon.Server;
using Napoleon.Server.Configuration;
using Napoleon.Server.PublishSubscribe;
using Napoleon.Server.PublishSubscribe.UdpImplementation;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;
using Serilog;
using Spectre.Console;

namespace Napoleon;

internal static class Program
{
    private static void ConfigToConsole(NodeConfiguration configuration)
    {
        AnsiConsole.WriteLine();

        var table = new Table();

        table.Title("[deepskyblue1]Configuration parameters[/]");

        table.AddColumn("param").AddColumn("value");
        table.AddRow("cluster", configuration.ClusterName!);
        table.AddRow("heart-beat period", $"{configuration.HeartbeatPeriodInMilliseconds} ms");
        table.AddRow("node-id policy", $"{configuration.NodeIdPolicy}");
        table.AddRow("data directory", $"{configuration.DataDirectory}");

        if (configuration.NodeIdPolicy == NodeIdPolicy.ExplicitName) table.AddRow("node-id", $"{configuration.NodeId}");

        table.AddRow("client port", $"{configuration.NetworkConfiguration.TcpClientPort}");
        table.AddRow("server2server", $"{configuration.NetworkConfiguration.ServerToServerProtocol}");

        if (configuration.NetworkConfiguration.ServerToServerProtocol == NotificationProtocol.UdpMulticast)
        {
            table.AddRow("multicast group", $"{configuration.NetworkConfiguration.MulticastAddress}");
            table.AddRow("multicast port", $"{configuration.NetworkConfiguration.MulticastPort}");
        }
        else
        {
            foreach (var server in configuration.NetworkConfiguration.ServerLists) table.AddRow("server", server);
        }


        AnsiConsole.Write(table);
    }

    private static async Task ServerStatusToConsole(ClusterCoordinator myServer, NodeConfiguration configuration)
    {
        var table = new Table();
        AnsiConsole.WriteLine();

        await AnsiConsole.Live(table).StartAsync(async ctx =>
        {
            table.Title("[deepskyblue1]Nodes in cluster[/]");
            table.AddColumn("");
            table.AddColumn("node");
            table.AddColumn("status");
            table.AddColumn("IP");
            table.AddColumn("port");
            table.AddColumn("data-version");
            table.AddColumn("alive");


            while (true)
            {
                Console.Title =
                    $"{myServer.MyStatus} in cluster {configuration.ClusterName} ( {myServer.NodesAliveInCluster} nodes alive)";

                table.Rows.Clear();


                foreach (var server in myServer.AllNodes())
                {
                    var alive = server.IsAlive;

                    var strAlive = alive ? "[green]alive[/]" : "[red]dead[/]";

                    var myself = server.NodeId == myServer.MyNodeId;

                    var strMyself = myself ? "*" : " ";

                    var status = alive ? server.StatusInCluster.ToString() : " ";

                    table.AddRow(strMyself, server.NodeId, status, server.TcpAddress, server.TcpClientPort.ToString(),
                        server.DataVersion.ToString(), strAlive);
                }


                ctx.Refresh();

                await Task.Delay(400);
            }
        });
    }

    private static ServiceProvider CreateServices(NodeConfiguration config)
    {
        // add the tcp port to the log file name name to make it unique for a machine
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File($"logs/log{config.NetworkConfiguration.TcpClientPort}.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();


        var publisher = new Publisher(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        
        var consumer = new Consumer(config.NetworkConfiguration.MulticastAddress!,
            config.NetworkConfiguration.MulticastPort);

        var dataStore = new DataStore();


        var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddSerilog(Log.Logger))
            .AddSingleton<ServerSuite>()
            .AddSingleton<ClusterCoordinator>()
            .AddSingleton<IPersistenceEngine, PersistenceEngine>()
            .AddSingleton<IPublisher>(publisher)
            .AddSingleton<IConsumer>(consumer)
            .AddSingleton<IReadOnlyDataStore>(dataStore)
            .AddSingleton<IDataStore>(dataStore)
            .AddSingleton(config)
            .AddSingleton(dataStore)
            .AddSingleton<DataServer>()
            .BuildServiceProvider();

        return serviceProvider;
    }

    private static async Task Main(string[] args)
    {
        try
        {
            var configurationFile = "config.json";
            if (args.Length > 0) configurationFile = args[0];
            // Load and display configuration
            var config = ConfigurationHelper.TryLoadFromFile(configurationFile);

            if (config == null)
            {
                AnsiConsole.Markup(
                    "[yellow] Can not load configuration from file[/] [underline yellow]config.json.[/] Using default!");
                config = ConfigurationHelper.CreateDefault("DEV");
            }

            config.CheckConfiguration();

            ConfigToConsole(config);

            var services = CreateServices(config);


            using var serverSuite = services.GetRequiredService<ServerSuite>();

            serverSuite.Start(config);


            // wait for cluster status update
            await Task.Delay(config.HeartbeatPeriodInMilliseconds * 2);


            await ServerStatusToConsole(serverSuite.ClusterServer!, config);

            Console.WriteLine("Stopping");
        }
        catch (Exception e)
        {
            AnsiConsole.Markup($"[yellow] Exception : [/] [underline yellow]{e.Message}[/]");
            
        }
    }
}