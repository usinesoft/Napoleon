using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe;

namespace Napoleon.Server;

public sealed class Server : IDisposable
{
    
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Dictionary<string, NodeStatus> _clusterState = new();

    private readonly string _myNodeId;

    private bool _disposed;

    private Task? _heartBeatThread;

    public Server(IPublisher publisher, IConsumer consumer)
    {
        Publisher = publisher;
        Consumer = consumer;

        _myNodeId = Guid.NewGuid().ToString();
    }

    private IPublisher Publisher { get; }

    private IConsumer Consumer { get; }

    public StatusInCluster MyStatus { get; private set; }

    public int NodesAliveInCluster { get; set; }


    public void Dispose()
    {
        _disposed = true;
        _cancellationTokenSource.Cancel();
        _heartBeatThread?.Wait(TimeSpan.FromMilliseconds(100));

        Publisher.Dispose();
        Consumer.Dispose();
    }

    public void Run(string cluster)
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
                    var hbMessage = MessageHelper.CreateHeartbeat(cluster, _myNodeId);
                    Publisher.Publish(hbMessage);

                    UpdateMyStatus();

                    await Task.Delay(TimeSpan.FromMilliseconds(NodeConfiguration.HeartbeatFrequencyInMilliseconds),
                        _cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore (cancelled by Dispose)
            }
        });

        Consumer.Start(cluster, _myNodeId);

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
                _clusterState[message.SenderNode!] = new(message.SenderNode!, message.SenderStatus);
            }

            UpdateMyStatus();
        }
    }

    private void UpdateMyStatus()
    {
        lock (_clusterState)
        {
            var livingNodes = _clusterState.Where(x => x.Value.IsAlive).Select(x => x.Key).ToList();
            livingNodes.Add(_myNodeId);

            NodesAliveInCluster = livingNodes.Count;

            if (livingNodes.Count == 1)
            {
                MyStatus = StatusInCluster.HomeAlone;
                return;
            }

            livingNodes.Sort();

            bool noOtherLeader = _clusterState.All(x=>x.Value.StatusInCluster != StatusInCluster.Leader);

            bool amICandidate = livingNodes[0] == _myNodeId;

            // Before switching to leader wait for a potential previous leader to resign
            // This is required to guarantee that only one leader is active at a time
            if (amICandidate)
            {
                MyStatus = noOtherLeader ? StatusInCluster.Leader : StatusInCluster.Candidate;
            }
            else
            {
                MyStatus = StatusInCluster.Follower;
            }

            
        }
    }
}