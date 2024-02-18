using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe;

namespace Napoleon.Server.Server
{
    public sealed class Server:IDisposable
    {
        public enum StatusInCluster
        {
            None,
            HomeAlone,
            Follower, 
            Leader
        }

        private IPublisher Publisher { get; }

        private IConsumer Consumer { get; }

        public Server(IPublisher publisher, IConsumer consumer)
        {
            Publisher = publisher;
            Consumer = consumer;

            _myNodeId = Guid.NewGuid().ToString();
        }

        private bool _disposed;

        private Task? _heartBeatThread;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly Dictionary<string, NodeStatus> _clusterState = new ();

        public StatusInCluster MyStatus { get; private set; }

        public int NodesAliveInCluster { get; set; }

        private readonly string _myNodeId;

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

                        await Task.Delay(TimeSpan.FromMilliseconds(NodeStatus.HeartBeatFrequencyInMilliseconds), _cancellationTokenSource.Token);

                        
                    }
                }
                catch (TaskCanceledException )
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
                    _clusterState[message.SenderNode!] = new NodeStatus(message.SenderNode!);
                    
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
                MyStatus = livingNodes[0] == _myNodeId ? StatusInCluster.Leader : StatusInCluster.Follower;
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
    }
}
