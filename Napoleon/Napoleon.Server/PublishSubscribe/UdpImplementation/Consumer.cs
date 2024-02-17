﻿using System.Net;
using System.Net.Sockets;
using Napoleon.Server.Messages;

namespace Napoleon.Server.PublishSubscribe.UdpImplementation;

public sealed class Consumer:IConsumer, IDisposable
{
    private readonly UdpClient _listener;
    
    private Task? _serverThread;
    
    readonly CancellationTokenSource _tokenSource = new();

    private bool _disposed;

    /// <summary>
    /// Time to live (number of accepted hoops)
    /// </summary>
    private const int Ttl = 32;
    
    /// <summary>
    /// Message ids stored to avoid duplicate messages
    /// </summary>
    private const int HistorySize = 10;

    /// <summary>
    /// Keep the last received message to avoid processing duplicate one
    /// </summary>
    private readonly Queue<int> _alreadyReceived = new();

    bool AlreadyReceived(int messageId)
    {
        lock (_alreadyReceived)
        {
            return _alreadyReceived.Contains(messageId);
        }
    }

    void StoreInHistory(int messageId)
    {
        lock (_alreadyReceived)
        {
            _alreadyReceived.Enqueue(messageId);
            if (_alreadyReceived.Count > HistorySize) // limit the queue size
            {
                _alreadyReceived.Dequeue();
            }
        }
    }


    public Consumer(int broadcastPort, string groupAddress)
    {
        var endpoint = new IPEndPoint(IPAddress.Any, broadcastPort);
        
        _listener = new(endpoint);
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
}