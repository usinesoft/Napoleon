using System.Net;
using System.Net.Sockets;
using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe;

namespace Napoleon.Server;

public sealed class Server : IDisposable
{
    private readonly NodeConfiguration _config;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Dictionary<string, NodeStatus> _clusterState = new();

    public string MyNodeId { get; }

    private bool _disposed;

    private Task? _heartBeatThread;
    private readonly string _myIpAddress;

    public static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        throw new NotSupportedException("No network adapters with an IPv4 address in the system!");
    }

    public Server(IPublisher publisher, IConsumer consumer, NodeConfiguration config)
    {
        _config = config;

        Publisher = publisher;
        Consumer = consumer;

        _myIpAddress = GetLocalIpAddress();

        MyNodeId = config.NodeIdPolicy switch
        {
            NodeIdPolicy.Guid => Guid.NewGuid().ToString(),
            NodeIdPolicy.ExplicitName => config.NodeId!,
            NodeIdPolicy.ImplicitIpAndPort => $"{_myIpAddress}:{config.NetworkConfiguration.TcpClientPort}",
            _ => throw new NotSupportedException("Invalid nodeIdPolicy in config")
        };
    }

    private IPublisher Publisher { get; }

    private IConsumer Consumer { get; }

    public StatusInCluster MyStatus { get; private set; }

    public int NodesAliveInCluster { get; private set; }

    public NodeStatus[] AllNodes()
    {
        lock (_clusterState)
        {
            var others = _clusterState!.Values!.ToList();
            others.Add(new NodeStatus(MyStatus, _config.HeartbeatPeriodInMilliseconds, _config.NetworkConfiguration.TcpClientPort, _myIpAddress, MyNodeId));
            return others.OrderBy(x=>x.NodeId).ToArray();
        }
     
    }


    public void Dispose()
    {
        _disposed = true;
        _cancellationTokenSource.Cancel();
        _heartBeatThread?.Wait(TimeSpan.FromMilliseconds(100));

        Publisher.Dispose();
        Consumer.Dispose();
    }

    public void Run()
    {
        MyStatus = StatusInCluster.HomeAlone;
        NodesAliveInCluster = 1; // myself


        // start the heartbeat publisher
        _heartBeatThread = Task.Run(async () =>
        {
            try
            {
                while (!_disposed)
                {
                    var hbMessage = MessageHelper.CreateHeartbeat(_config, MyNodeId, MyStatus, _myIpAddress);
                    Publisher.Publish(hbMessage);

                    UpdateMyStatus();

                    await Task.Delay(TimeSpan.FromMilliseconds(_config.HeartbeatPeriodInMilliseconds),
                        _cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore (cancelled by Dispose)
            }
        });

        Consumer.Start(_config.ClusterName!, MyNodeId);

        // listen for other heart-beats in the cluster
        Consumer.MessageReceived += MessageReceived;
    }

    private void MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        var message = e.MessageHeader;
        if (message.MessageType == MessageType.Heartbeat && message.IsValidHeartbeat())
        {
            lock (_clusterState)
            {
                var nodeStatus = new NodeStatus(message.SenderStatus, message.HeartbeatPeriodInMilliseconds,
                    message.SenderPortForClients, message.SenderIp!, message.SenderNode);

                
                _clusterState[message.SenderNode!] = nodeStatus;
            }

            UpdateMyStatus();
        }
    }

    private void UpdateMyStatus()
    {
        lock (_clusterState)
        {
            var livingNodes = _clusterState.Where(x => x.Value.IsAlive).Select(x => x.Key).ToList();
            livingNodes.Add(MyNodeId);

            NodesAliveInCluster = livingNodes.Count;

            if (livingNodes.Count == 1)
            {
                MyStatus = StatusInCluster.HomeAlone;
                return;
            }

            livingNodes.Sort();

            var noOtherLeader = _clusterState.Where(x=>x.Value.IsAlive).All(x => x.Value.StatusInCluster != StatusInCluster.Leader);

            var amICandidate = livingNodes[0] == MyNodeId;

            // Before switching to leader wait for a potential previous leader to resign
            // This is required to guarantee that only one leader is active at a time
            if (amICandidate)
                MyStatus = noOtherLeader ? StatusInCluster.Leader : StatusInCluster.Candidate;
            else
                MyStatus = StatusInCluster.Follower;

            // cleanup long dead nodes
            HashSet<string> nodesToForget = new(_clusterState.Where(x=>x.Value.IsForgotten).Select(x=>x.Key));
            foreach (var node in nodesToForget)
            {
                _clusterState.Remove(node);
            }

        }
    }
}