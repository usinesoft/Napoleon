using System.Net;
using System.Net.Sockets;
using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe.UdpImplementation;

public sealed class Consumer : IConsumer
{
    /// <summary>
    ///     Time to live (number of accepted hoops)
    /// </summary>
    private const int Ttl = 32;

    /// <summary>
    ///     Message ids stored to avoid duplicate messages
    /// </summary>
    private const int HistorySize = 10;

    /// <summary>
    ///     Keep the last received message to avoid processing duplicate one
    /// </summary>
    private readonly Queue<int> _alreadyReceived = new();

    private readonly UdpClient _listener;

    private readonly CancellationTokenSource _tokenSource = new();

    private bool _disposed;

    private Task? _serverThread;


    public Consumer(string groupAddress, int broadcastPort)
    {
        var endpoint = new IPEndPoint(IPAddress.Any, broadcastPort);

        _listener = new(AddressFamily.InterNetwork);
        _listener.ExclusiveAddressUse = false;
        _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.MulticastLoopback = true;
        _listener.Client.Bind(endpoint);
        _listener.JoinMulticastGroup(IPAddress.Parse(groupAddress), Ttl);
    }


    public void Start(string clusterName, string nodeId)
    {
        _serverThread = Task.Factory.StartNew(async () =>
        {
            try
            {
                while (!_disposed)
                {
                    var result = await _listener.ReceiveAsync(_tokenSource.Token);

                    var data = result.Buffer;

                    var message = new MessageHeader();

                    message.FromRawBytes(data);

                    // ignore messages from other clusters and from myself
                    if (message.Cluster == clusterName && message.SenderNode != nodeId)
                    {
                        // ignore duplicate messages
                        if (AlreadyReceived(message.MessageId)) continue;

                        StoreInHistory(message.MessageId);
                        MessageReceived?.Invoke(this, new(message));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }).Result;
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public void Dispose()
    {
        _disposed = true;

        _tokenSource.Cancel();

        _serverThread?.Wait(TimeSpan.FromMilliseconds(50));

        _listener.Dispose();
    }

    private bool AlreadyReceived(int messageId)
    {
        lock (_alreadyReceived)
        {
            return _alreadyReceived.Contains(messageId);
        } 
    }

    private void StoreInHistory(int messageId)
    {
        lock (_alreadyReceived)
        {
            _alreadyReceived.Enqueue(messageId);
            if (_alreadyReceived.Count > HistorySize) // limit the queue size
                _alreadyReceived.Dequeue();
        }
    }
}