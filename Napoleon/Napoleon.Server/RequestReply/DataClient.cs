using System.Text.Json;
using System.Text.Json.Nodes;

namespace Napoleon.Server.RequestReply;

/// <summary>
/// Client specialized for data requests.
/// </summary>
public sealed class DataClient:IDisposable
{
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly RawClient _rawClient;

    public DataClient(string serverAddress, int serverPort)
    {
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _rawClient = new RawClient();
    }

    public void Connect()
    {
        if (!_rawClient.TryConnect(_serverAddress, _serverPort))
        {
            throw new NotSupportedException($"Can not connect to {_serverAddress}:{_serverPort}");
        }
    }

    public async Task<JsonElement> GetValue(string collection, string key, CancellationToken ct)
    {

        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.GetData,
            ["collection"] = collection,
            ["key"] = key,
        };

        return await _rawClient.RequestOne(request, ct);

    }

    public async Task<JsonElement> DeleteValue(string collection, string key, CancellationToken ct)
    {

        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.DeleteData,
            ["collection"] = collection,
            ["key"] = key,
        };

        return await _rawClient.RequestOne(request, ct);

    }

    public async Task<JsonElement> PutValue(string collection, string key, JsonNode value, CancellationToken ct)
    {
        
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.PutData,
            ["collection"] = collection,
            ["key"] = key,
            ["value"] = value
        };

        return await _rawClient.RequestOne(request, ct);
    }

    public async Task<JsonElement> PutValue(string collection, string key, object value, CancellationToken ct)
    {
        
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.PutData,
            ["collection"] = collection,
            ["key"] = key,
            ["value"] = JsonSerializer.SerializeToNode(value)
        };

        return await _rawClient.RequestOne(request, ct);
    }


    /// <summary>
    /// For test only. Trigger an exception on the server
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<JsonElement> SimulateException(CancellationToken ct)
    {
        
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.RaiseExceptionForTests,
            
        };

        return await _rawClient.RequestOne(request, ct);
    }

    public void Dispose()
    {
        _rawClient.Dispose();
    }
}