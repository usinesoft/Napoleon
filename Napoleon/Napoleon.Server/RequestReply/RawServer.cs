using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe.TcpImplementation;

// because we have very good reasons to spawn a detached Task
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Napoleon.Server.RequestReply;

/// <summary>
///     This server manipulates Json messages. The actual message processing is delegated to an external component
///     through <see cref="RegisterRequestHandler" /> method. By convention the received request must contain the
///     "requestType"
///     numeric property which is used to route them to the correct handler
/// </summary>
public sealed class RawServer : IDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();

    private readonly Dictionary<int, Func<JsonDocument, JsonElement>> _handlersByRequestType = new();

    private bool _started;

    /// <summary>
    ///     A handler reads a <see cref="JsonDocument" /> and produces a <see cref="JsonElement" /> which is mutable.)
    ///     The <see cref="JsonElement" /> may be "undefined" if the response is not required
    /// </summary>
    /// <param name="requestType"></param>
    /// <param name="requestHandler"></param>
    /// <exception cref="NotSupportedException"></exception>
    public void RegisterRequestHandler(int requestType, Func<JsonDocument, JsonElement> requestHandler)
    {
        if (_started) throw new NotSupportedException("RegisterHandler should be called before the server is started");

        _handlersByRequestType[requestType] = requestHandler;
    }

    /// <summary>
    ///     Deserialize client requests as <see cref="JsonDocument" /> dispatch them to the external handler
    ///     and send back to the client a response produced by the handler.
    ///     In case an exception is thrown inside the handler, send an exception response
    /// </summary>
    /// <param name="client"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private async Task ClientLoop(TcpClient client, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var stream = client.GetStream();

                var requestSize = await stream.ReadIntAsync(ct);
                var requestData = await stream.ReadDataAsync(requestSize, ct);

                var request = JsonSerializer.Deserialize<JsonDocument>(requestData);
                if (request == null) throw new NotSupportedException("The request is not a valid json document");

                var requestType = request.RequestType();

                if (!_handlersByRequestType.TryGetValue(requestType, out var handler))
                    throw new NotSupportedException($"Handler not found for request type {requestType}");


                string? jsonResponse = null;
                try
                {
                    // call the handler
                    var response = handler(request);

                    if (response.ValueKind == JsonValueKind.Undefined)
                    {
                        await stream.WriteIntAsync(0, ct);
                    }
                    else
                    {
                        jsonResponse = JsonSerializer.Serialize(response);
                        
                    }
                }
                catch (Exception e)
                {
                    //send an exception response if the handler throws an exception
                    var exceptionResponse = new JsonObject
                    {
                        [RequestConstants.PropertyNameIsException] = true,
                        [RequestConstants.PropertyNameExceptionMessage] = e.Message
                    };
                    jsonResponse = JsonSerializer.Serialize(exceptionResponse);

                }

                if (jsonResponse != null)
                {
                    var data = Encoding.UTF8.GetBytes(jsonResponse);
                    await stream.WriteIntAsync(data.Length, ct);
                    await stream.WriteAsync(data, ct);
                }
                
            }
        }
        catch (IOException)
        {
            //ignore
        }
        catch (TaskCanceledException)
        {
            //ignore
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    ///     Wait for clients and spawn an async task for each new client
    /// </summary>
    /// <param name="port"></param>
    /// <returns></returns>
    public Task Start(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);

        listener.Start();
        _started = true;

        return Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync(_tokenSource.Token);


                    Task.Run(async () => { await ClientLoop(client, _tokenSource.Token); });
                }
            }
            catch (TaskCanceledException)
            {
                //ignore (received when disposed only)
            }
        });
    }

    public void Dispose()
    {
        _tokenSource.Cancel();
        _tokenSource.Dispose();
    }
}