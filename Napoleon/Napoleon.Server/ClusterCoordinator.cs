using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Napoleon.Server.Configuration;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace Napoleon.Server;

/// <summary>
///     This class represents a server responsible for leader election and data synchronization
/// </summary>
public class ClusterCoordinator : IDisposable, ICoordinator
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Dictionary<string, NodeStatus> _clusterState = new();

    private readonly NodeConfiguration _config;

    private readonly ILogger<ClusterCoordinator> _logger;
    private readonly IPersistenceEngine _persistenceEngine;

    private readonly IDataStore _dataStore;

    /// <summary>
    ///     Used to guarantee that only one data synchronization is running at a time
    /// </summary>
    private readonly SemaphoreSlim _dataSyncSemaphore = new(1);

    private readonly string _myIpAddress;

    /// <summary>
    /// Clients that wait for a wake-up call if data changes
    /// </summary>
    private readonly List<WakeUpCall> _waitingForWakeUp = new();

    private bool _disposed;

    private Task? _heartBeatThread;

    private StatusInCluster _myStatus;

    public ClusterCoordinator(IPublisher publisher, IConsumer consumer, IDataStore dataStore, NodeConfiguration config, ILogger<ClusterCoordinator> logger, IPersistenceEngine persistenceEngine)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _persistenceEngine = persistenceEngine;

        config.CheckConfiguration();

        _myIpAddress = GetLocalIpAddress();

        // if 0 in config (dynamic port) it will be set later when data server starts
        DataPort = _config.NetworkConfiguration.TcpClientPort;

        Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));

        _dataStore.AfterDataChanged += AfterDataChanged;
    }

    private IPublisher Publisher { get; }

    private IConsumer Consumer { get; }

    public int NodesAliveInCluster { get; private set; }
    public int DataPort { get; set; }


    public void Dispose()
    {
        _logger.LogDebug("Disposing cluster coordinator");    

        _disposed = true;
        _cancellationTokenSource.Cancel();
        _heartBeatThread?.Wait(TimeSpan.FromMilliseconds(100));

        Publisher.Dispose();
        Consumer.Dispose();

        _logger.LogDebug("Disposed cluster coordinator");    
    }

    public string? MyNodeId { get; private set; }

    public StatusInCluster MyStatus
    {
        get => _myStatus;
        private set
        {
            if (value != _myStatus)
            {
                _logger.LogInformation($"My status changed from {_myStatus} to {value}");
                _myStatus = value;
                
            }
            
        }
    }

    public NodeStatus[] AllNodes()
    {
        lock (_clusterState)
        {
            var others = _clusterState.Values.ToList();
            others.Add(new(MyStatus, _config.HeartbeatPeriodInMilliseconds, _config.NetworkConfiguration.TcpClientPort,
                _myIpAddress, MyNodeId!, _dataStore.GlobalVersion));
            return others.OrderBy(x => x.NodeId).ToArray();
        }
    }

    /// <summary>
    ///     Waits for the current data synchronization (if one is pending) to finish
    /// </summary>
    /// <returns></returns>
    public async Task WaitSyncingEnd()
    {
        while (true)
        {
            if (_dataSyncSemaphore.CurrentCount == 1) return;

            await Task.Delay(100, _cancellationTokenSource.Token);
        }
    }

    /// <summary>
    ///     Called by a client who wants to wait for data change
    /// </summary>
    /// <param name="wakeUpCall"></param>
    public void WakeMeUpWhenDataChanged(WakeUpCall wakeUpCall)
    {
        lock (_waitingForWakeUp)
        {
            _waitingForWakeUp.Add(wakeUpCall);
        }
    }

    public static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        throw new NotSupportedException("No network adapters with an IPv4 address in the system!");
    }

    /// <summary>
    ///     Only the leader sends this message to other nodes in the cluster
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AfterDataChanged(object? sender, DataChangedEventArgs e)
    {
        _logger.LogDebug("After data change");

        if (MyStatus != StatusInCluster.Leader) return; // this should never happen

        // publish unitary data change to allow the other nodes to synchronize fast
        Publisher.Publish(MessageHelper.CreateDataSyncMessage(_config.ClusterName!, MyNodeId!,
            new List<Item> { e.ChangedData }));

        // wake up the clients waiting for data change
        WakeUpEverybody();
    }

    private void WakeUpEverybody()
    {
        lock (_waitingForWakeUp)
        {
            foreach (var wakeUpCall in _waitingForWakeUp) wakeUpCall.WakeUp();

            _waitingForWakeUp.Clear();
        }
    }


    public void Start()
    {

        _logger.LogDebug("Starting cluster coordinator");

        MyNodeId = _config.NodeIdPolicy switch
        {
            NodeIdPolicy.Guid => Guid.NewGuid().ToString(),
            NodeIdPolicy.ExplicitName => _config.NodeId,
            NodeIdPolicy.ImplicitIpAndPort => $"{_myIpAddress}:{DataPort}",
            _ => throw new NotSupportedException("Invalid nodeIdPolicy in config")
        };


        MyStatus = StatusInCluster.HomeAlone;
        NodesAliveInCluster = 1; // myself


        // start the heartbeat publisher
        _heartBeatThread = Task.Run(async () =>
        {
            try
            {
                while (!_disposed)
                {
                    var hbMessage = MessageHelper.CreateHeartbeat(_config, MyNodeId!, MyStatus, _myIpAddress);
                    hbMessage.SenderPortForClients = DataPort;
                    hbMessage.DataVersion = _dataStore.GlobalVersion;

                    Publisher.Publish(hbMessage);

                    await UpdateMyStatus();

                    await Task.Delay(TimeSpan.FromMilliseconds(_config.HeartbeatPeriodInMilliseconds),
                        _cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // ignore (cancelled by Dispose)
            }
        });

        _logger.LogDebug("Starting consumer");

        Consumer.Start(_config.ClusterName!, MyNodeId!);

        // listen for other notifications in the cluster
        Consumer.MessageReceived += MessageReceived;
    }

    private void MessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        var message = e.MessageHeader;

        
        // Two types of notifications can be received: regular heart-beat and data synchronization requests 
        // sent by the leader (the leader never receives this message)

        switch (message.MessageType)
        {
            case MessageType.Heartbeat when message.IsValidHeartbeat():
            {
                lock (_clusterState)
                {
                    var nodeStatus = new NodeStatus(message.SenderStatus, message.HeartbeatPeriodInMilliseconds,
                        message.SenderPortForClients, message.SenderIp!, message.SenderNode!, message.DataVersion);


                    _clusterState[message.SenderNode!] = nodeStatus;
                }

                break;
            }
            case MessageType.DataSync:
            {
                _logger.LogInformation("Data sync message received");

                var payload = new DataSyncPayload();
                payload.FromRawBytes(message.Payload);

                if (payload.Items.Count == 1) // this message contains a single item (unitary change)
                {
                    if (_config.DataDirectory != null)
                    {
                        _persistenceEngine.SaveChange(payload.Items[0], _config.DataDirectory);
                    }
                    
                    _dataStore.TryApplyAsyncChange(payload.Items[0]);
                }
                    

                WakeUpEverybody();

                break;
            }
        }
    }

    /// <summary>
    ///     Explicit data synchronization. Triggered when the asynchronous notification was missed
    /// </summary>
    /// <returns></returns>
    private async Task SynchronizeData(NodeStatus chosenNode)
    {
        _logger.LogInformation("Synchronizing data");

        var dataVersionBefore = _dataStore.GlobalVersion;

        // avoid two synchronizations in parallel
        await _dataSyncSemaphore.WaitAsync();

        try
        {
            var client = new DataClient();
            client.Connect(chosenNode.TcpAddress, chosenNode.TcpClientPort);

            var changes = new List<Item>();
            await foreach (var change in client.GetAllChangesSinceVersion(_dataStore.GlobalVersion))
            {
                changes.Add(change);

                if (_config.DataDirectory != null)
                {
                    _persistenceEngine.SaveChange(change, _config.DataDirectory);
                }
                
            }
                

            _logger.LogInformation($"Applying {changes.Count} changes");

            _dataStore.ApplyChanges(changes);
        }
        finally
        {
            _dataSyncSemaphore.Release();

            var dataVersionAfter = _dataStore.GlobalVersion;
            if (dataVersionAfter > dataVersionBefore) WakeUpEverybody();
        }
    }


    /// <summary>
    ///     Randomly choose a node in the cluster that is synchronized (has the most recent version of data)
    /// </summary>
    /// <returns></returns>
    private NodeStatus? ChooseUpToDateNode()
    {
        // get a list of synchronized nodes
        IList<NodeStatus> upToDateNodes = new List<NodeStatus>();

        lock (_clusterState)
        {
            if (_clusterState.Count == 0) return null;


            // get the most recent data version in the cluster
            var maxVersion = _clusterState.Max(x => x.Value.DataVersion);

            foreach (var node in _clusterState.Values)
                if (node.DataVersion == maxVersion)
                    upToDateNodes.Add(node);
        }

        NodeStatus chosenNode;

        if (upToDateNodes.Count == 1)
        {
            chosenNode = upToDateNodes[0];
        }
        else
        {
            var random = new Random();
            chosenNode = upToDateNodes[random.Next(upToDateNodes.Count)];
        }

        return chosenNode;
    }


    /// <summary>
    ///     This method is called when something changes in the cluster.
    ///     Two cases:
    ///     1) The current node is synchronized (same data version as the most recent one in the cluster)
    ///     In this case check if a leader election is required
    ///     2) The current node does not have the last version of data.
    ///     In this case trigger an explicit synchronization
    /// </summary>
    private async Task UpdateMyStatus()
    {
        var upToDate = ChooseUpToDateNode();

        if (upToDate == null) return; // I am alone. Nothing to do

        if (upToDate.DataVersion > _dataStore.GlobalVersion) // My data is not up to date
            await SynchronizeData(upToDate);
        else // My data is up to date. Leader election is possible
            DoPolitics();
    }

    /// <summary>
    ///     Check if my status changed (eventually candidate for leadership)
    /// </summary>
    private void DoPolitics()
    {
        lock (_clusterState)
        {
            var livingNodes = _clusterState.Where(x => x.Value.IsAlive).Select(x => x.Key).ToList();
            livingNodes.Add(MyNodeId!);

            NodesAliveInCluster = livingNodes.Count;

            if (livingNodes.Count == 1)
            {
                MyStatus = StatusInCluster.HomeAlone;
                return;
            }

            livingNodes.Sort();

            var noOtherLeader = _clusterState.Where(x => x.Value.IsAlive)
                .All(x => x.Value.StatusInCluster != StatusInCluster.Leader);


            var amICandidate = livingNodes[0] == MyNodeId;

            
            // Before switching to leader wait for a potential previous leader to resign
            // This is required to guarantee that only one leader is active at a time
            if (amICandidate)
                MyStatus = noOtherLeader ? StatusInCluster.Leader : StatusInCluster.Candidate;
            else
                MyStatus = StatusInCluster.Follower;

            // cleanup long dead nodes
            HashSet<string> nodesToForget = new(_clusterState.Where(x => x.Value.IsForgotten).Select(x => x.Key));
            foreach (var node in nodesToForget) _clusterState.Remove(node);
        }
    }
}