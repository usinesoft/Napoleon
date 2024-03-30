using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Napoleon.Server.Messages;
using Napoleon.Server.PublishSubscribe.TcpImplementation;
using Napoleon.Server.SharedData;

// because we have very good reasons to spawn a detached Task
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Napoleon.Server.RequestReply;

/// <summary>
///     This server manipulates Json messages. The actual message processing is delegated to an external component
///     through <see cref="RegisterRequestHandler" /> method. By convention the received request must contain the
///     "requestType" numeric property. It will be used to route the request to the specific handler
/// </summary>
public sealed class RawServer : IDisposable
{
    /// <summary>
    ///     Simple handlers (one input, one output)
    /// </summary>
    private readonly Dictionary<int, Func<JsonDocument, Task<string?>>> _handlersByRequestType = new();

    /// <summary>
    ///     Complex handler that can send a stream of responses
    /// </summary>
    private readonly Dictionary<int, Func<JsonDocument, IAsyncEnumerable<string>>>
        _streamingHandlersByRequestType = new();

    private readonly CancellationTokenSource _tokenSource = new();

    private bool _started;

    public void Dispose()
    {
        _tokenSource.Cancel();
        _tokenSource.Dispose();
    }

    /// <summary>
    ///     A handler reads a <see cref="JsonDocument" /> and produces a string (which may be null for a void response)
    /// </summary>
    /// <param name="requestType"></param>
    /// <param name="requestHandler"></param>
    /// <exception cref="NotSupportedException"></exception>
    public void RegisterRequestHandler(int requestType, Func<JsonDocument, Task<string?>> requestHandler)
    {
        if (requestType > RequestConstants.StreamingRequestTypesStartAt)
            throw new ArgumentOutOfRangeException(nameof(requestType),
                $"For simple handlers requestType must be less than {RequestConstants.StreamingRequestTypesStartAt} ");

        if (_started) throw new NotSupportedException("RegisterHandler should be called before the server is started");


        _handlersByRequestType[requestType] = requestHandler;
    }

    /// <summary>
    ///     A handler reads a <see cref="JsonDocument" /> and produces a stream of responses as strings (usually Json)
    /// </summary>
    /// <param name="requestType"></param>
    /// <param name="streamingHandler"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public void RegisterStreamingRequestHandler(int requestType,
        Func<JsonDocument, IAsyncEnumerable<string>> streamingHandler)
    {
        if (requestType < RequestConstants.StreamingRequestTypesStartAt)
            throw new ArgumentOutOfRangeException(nameof(requestType),
                $"For streaming handlers requestType must be greater than {RequestConstants.StreamingRequestTypesStartAt} ");

        if (_started) throw new NotSupportedException("RegisterHandler should be called before the server is started");

        _streamingHandlersByRequestType[requestType] = streamingHandler;
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

                var request = JsonSerializer.Deserialize(requestData, SerializationContext.Default.JsonDocument);
                if (request == null) throw new NotSupportedException("The request is not a valid json document");

                var requestType = request.RequestType();

                if (requestType < RequestConstants.StreamingRequestTypesStartAt)
                    await ProcessSimpleRequest(requestType, request, stream, ct);
                else // streaming request
                    await ProcessStreamingRequest(requestType, request, stream, ct);
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
    ///     Process a request having a simple response
    /// </summary>
    /// <param name="requestType"></param>
    /// <param name="request"></param>
    /// <param name="stream"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private async Task ProcessSimpleRequest(int requestType, JsonDocument request,
        NetworkStream stream, CancellationToken ct)
    {
        if (!_handlersByRequestType.TryGetValue(requestType, out var handler))
            throw new NotSupportedException($"Handler not found for request type {requestType}");


        string? response;

        try
        {
            // call the handler
            response = await handler(request);

            if (response == null)
                await stream.WriteIntAsync(0, ct);
        }
        catch (Exception e)
        {
            //send an exception response if the handler throws an exception

            response = e.ToExceptionResponseJson();
        }

        if (response != null)
        {
            var data = Encoding.UTF8.GetBytes(response);
            await stream.WriteIntAsync(data.Length, ct);
            await stream.WriteAsync(data, ct);
        }
    }

    /// <summary>
    ///     Process a request having a stream of responses
    /// </summary>
    /// <param name="requestType"></param>
    /// <param name="request"></param>
    /// <param name="stream"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private async Task ProcessStreamingRequest(int requestType, JsonDocument request,
        NetworkStream stream, CancellationToken ct)
    {
        if (!_streamingHandlersByRequestType.TryGetValue(requestType, out var handler))
            throw new NotSupportedException($"Handler not found for request type {requestType}");


        try
        {
            // call the handler
            await foreach (var result in handler(request))
            {
                var data = Encoding.UTF8.GetBytes(result);
                await stream.WriteIntAsync(data.Length, ct);
                await stream.WriteAsync(data, ct);
            }
        }
        catch (Exception e)
        {
            //send an exception response if the handler throws an exception

            var jsonResponse = e.ToExceptionResponseJson();
            var data = Encoding.UTF8.GetBytes(jsonResponse);
            await stream.WriteIntAsync(data.Length, ct);
            await stream.WriteAsync(data, ct);
        }
        finally // write end of stream
        {
            // As the items count is unknown we send a stream with suffix 
            // Expecting a size and reading 0 means the iteration ended
            await stream.WriteIntAsync(0, ct);
        }
    }

    /// <summary>
    ///     Wait for clients and spawn an async task for each new client
    /// </summary>
    /// <param name="port">A fixer port number or 0 to select a free one</param>
    /// <returns>The effective listening port if 0 was specified</returns>
    public int Start(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);

        listener.Start();
        _started = true;

        Task.Run(async () =>
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

        // if port 0 was specified a dynamic one was selected
        var dynamicPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        return dynamicPort;
    }
}