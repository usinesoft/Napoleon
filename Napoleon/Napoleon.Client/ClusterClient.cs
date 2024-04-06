using System.Runtime.Serialization;
using System.Text.Json;
using Napoleon.Server;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;
using Napoleon.Server.Tools;

namespace Napoleon.Client;

/// <summary>
///     A client for the distributed data mesh. It connects to a random server from a predefined list.
///     It has a local copy of the data which is maintained synchronized by a background thread.
///     In case of connection loss it silently reconnects to a random available server from the last known
///     list of available servers.
///     For data modification it connects to the leader of the cluster.
/// </summary>
public sealed class ClusterClient : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly List<NodeStatus> _clusterStatus = new();

    private readonly DataStore _myCopyOfData = new();

    private readonly object _statusLock = new();

    /// <summary>
    ///     Maximum retries if first connection fails
    /// </summary>
    private const int ConnectionAttempts = 5;

    private NormalizedAddress? _lastSuccessfulConnection;
    private RawClient? _rawClient;

    /// <summary>
    ///     Last known cluster status. A list of active servers with:
    ///     - their tcp address and port
    ///     - their role in the cluster (Leader or Follower)
    ///     - the version of their data
    /// </summary>
    public List<NodeStatus> ClusterStatus
    {
        get
        {
            lock (_statusLock)
            {
                return [.._clusterStatus];
            }
        }
    }

    public IReadOnlyDataStore Data => _myCopyOfData;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _rawClient?.Dispose();
    }

    private async Task<bool> TryConnect(string[] servers)
    {
        if (servers.Length == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(servers));

        var connected = false;

        for (var i = 0; i < ConnectionAttempts; i++)
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            await Task.Delay(i * 200);

            var server = servers.SelectRandom();

            var normalized = NormalizedAddress.FromString(server!);


            if (TryConnectTo(normalized.Host, normalized.Port))
            {
                connected = true;
                _lastSuccessfulConnection = normalized;

                ConnectionChanged?.Invoke(this, new ConnectionChanged($"{normalized.Host}:{normalized.Port}"));
                break;
            }
        }

        return connected;
    }

    private bool TryConnectTo(string host, int port)
    {
        return _rawClient!.TryConnect(host, port);
    }

    /// <summary>
    ///     Connect to the cluster. At least one hostname:port should be provided. Any node in the cluster is ok
    /// </summary>
    /// <param name="servers"></param>
    public async Task Connect(params string[] servers)
    {
        _rawClient = new();

        var connected = await TryConnect(servers);

        if (connected)
        {
            await GetClusterStatus();
            await SynchronizeData();

            StartBackgroundSynchronization();
        }
        else
        {
            throw new NotSupportedException("Can not connect to any of the specified servers");
        }
    }


    /// <summary>
    ///     Get a data client for write operations
    /// </summary>
    /// <returns></returns>
    public async Task<DataClient> GetLeaderDataClient()
    {
        var rawClient = await TryConnectToLeader();

        return new(rawClient);
    }

    /// <summary>
    ///     Connect to the leader of the cluster
    ///     If the first attempt is not successful try multiple times. The leader might
    ///     have changed or an election is pending so no active leader.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private async Task<RawClient> TryConnectToLeader()
    {
        var connected = false;
        var leaderConnection = new RawClient();


        for (var i = 0; i < ConnectionAttempts; i++)
        {
            await Task.Delay(100 * i);

            
            var leader = ClusterStatus.Find(x => x.StatusInCluster == StatusInCluster.Leader);
            if (leader != null) connected = leaderConnection.TryConnect(leader.TcpAddress, leader.TcpClientPort);

            if (connected)
                break;

            await GetClusterStatus(); // in case the leader has changed
        }

        if (connected) return leaderConnection;

        throw new NotSupportedException("Can not establish leader connection");
    }

    /// <summary>
    ///     Get the list of servers in the cluster.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="SerializationException"></exception>
    private async Task GetClusterStatus()
    {
        var response = await _rawClient!.RequestOne(new()
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.GetClusterStatus
        });

        if (response.ValueKind != JsonValueKind.Array)
            throw new NotSupportedException("Invalid response when getting cluster status");

        var all = response.Deserialize(SerializationContext.Default.NodeStatusArray);
        if (all == null) throw new SerializationException("Can not deserialize node status array");


        lock (_statusLock)
        {
            ClusterStatus.Clear();
            foreach (var node in all) _clusterStatus.Add(node);
        }
    }

    /// <summary>
    ///     A background thread that awaits for data-changes on the server and silently
    ///     updates the local copy of data.
    /// </summary>
    private void StartBackgroundSynchronization()
    {
        Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var dataClient = new DataClient(_rawClient!);

                    var changes = new List<Item>();

                    // Blocking call. It returns only if data changed or connection was lost
                    await foreach (var change in dataClient.GetAllChangesSinceVersion(_myCopyOfData.GlobalVersion, true,
                                       _cancellationTokenSource.Token))
                        changes.Add(change);

                    _myCopyOfData.ApplyChanges(changes);
                }
            }
            catch (IOException)
            {
                // in case of IO error reconnect to the cluster
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // avoid reconnecting to the current one
                    var otherNodes = ClusterStatus.Where(x =>
                        x.TcpAddress != _lastSuccessfulConnection!.Host &&
                        x.TcpClientPort != _lastSuccessfulConnection.Port);

                    var servers = otherNodes.Select(x => $"{x.TcpAddress}:{x.TcpClientPort}");
                    await Connect(servers.ToArray());
                }
            }
        });
    }

    private async Task SynchronizeData()
    {
        var dataClient = new DataClient(_rawClient!);
        var changes = new List<Item>();

        await foreach (var change in dataClient.GetAllChangesSinceVersion(_myCopyOfData.GlobalVersion))
            changes.Add(change);

        _myCopyOfData.ApplyChanges(changes);
    }

    public event EventHandler<ConnectionChanged> ConnectionChanged;
}


public class ConnectionChanged : EventArgs
{
    public ConnectionChanged(string connectionInfo)
    {
        ConnectionInfo = connectionInfo;
    }

    public string ConnectionInfo { get; private set; }
}