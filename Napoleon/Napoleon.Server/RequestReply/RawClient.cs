using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Napoleon.Server.PublishSubscribe.TcpImplementation;

namespace Napoleon.Server.RequestReply;

/// <summary>
/// Low level client that sends and receives data as Json
/// Requests are <see cref="JsonObject"/> which is mutable for caller's convenience
/// Single responses are <see cref="JsonElement"/> which is immutable and can be "undefined" (unlike <see cref="JsonDocument"/>)
/// An undefined response should be regarded as a void (no content required).
/// Streaming responses are strings for maximum efficiency.
/// </summary>
public sealed class RawClient : IDisposable
{
    private readonly TcpClient _client = new();

    /// <summary>
    ///     Connect to a server with timeout
    /// </summary>
    /// <param name="address"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public bool TryConnect(string address, int port)
    {
        try
        {
            _client.Connect(address, port);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }


    /// <summary>
    ///     To be used for a sequence of responses
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> RequestMany(JsonObject request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tcpStream = await SendRequest(request, ct);

        ct.ThrowIfCancellationRequested();

        
        await foreach (var item in tcpStream.FromStreamWithSuffixAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            yield return Encoding.UTF8.GetString(item);
           
        }
    }

    /// <summary>
    ///     Send a request as Json document. The numerical property "requestType" is mandatory.
    /// </summary>
    /// <param name="request">mutable json document (for caller's convenience)</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task<JsonElement> RequestOne(JsonObject request, CancellationToken ct = default)
    {
        var tcpStream = await SendRequest(request, ct);

        // response
        var responseSize = await tcpStream.ReadIntAsync(ct);
        if (responseSize == 0)
            // Returns a JSonElement with ValueKind  "undefined". This acts as a void response
            return default;

        var responseData = await tcpStream.ReadDataAsync(responseSize, ct);
        var response = JsonSerializer.Deserialize<JsonElement>(responseData);

        response.ThrowIfExceptionResponse();

        return response;
    }

    /// <summary>
    ///     Convert Json request to wire format and send it
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task<NetworkStream> SendRequest(JsonObject request, CancellationToken ct)
    {
        var reqAsDoc = request.Deserialize<JsonDocument>();

        if (reqAsDoc == null) throw new ArgumentException("Is not a valid Json document", nameof(request));
        
        var tcpStream = _client.GetStream();
        
        var json = JsonSerializer.Serialize(reqAsDoc);
        var data = Encoding.UTF8.GetBytes(json);

        await tcpStream.WriteIntAsync(data.Length, ct);
        await tcpStream.WriteAsync(data, ct);
        return tcpStream;
    }


    public void Dispose()
    {
        _client.Dispose();
    }
}