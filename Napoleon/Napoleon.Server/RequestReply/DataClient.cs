using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.RequestReply;

/// <summary>
///     Client specialized for data requests.
/// </summary>
public sealed class DataClient : IDisposable
{
    private readonly RawClient _rawClient;

    public DataClient()
    {
        _rawClient = new();
    }

    /// <summary>
    ///     When reusing an existing connection
    /// </summary>
    /// <param name="rawClient"></param>
    public DataClient(RawClient rawClient)
    {
        _rawClient = rawClient ?? throw new ArgumentNullException(nameof(rawClient));
    }

    public void Dispose()
    {
        _rawClient.Dispose();
    }


    public void Connect(string serverAddress, int serverPort)
    {
        if (!_rawClient.TryConnect(serverAddress, serverPort))
            throw new NotSupportedException($"Can not connect to {serverAddress}:{serverPort}");
    }

    public async Task<JsonElement> GetValue(string collection, string key, CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.GetData,
            ["collection"] = collection,
            ["key"] = key
        };

        return await _rawClient.RequestOne(request, ct);
    }

    public async Task<JsonElement> DeleteValue(string collection, string key, CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.DeleteData,
            ["collection"] = collection,
            ["key"] = key
        };

        return await _rawClient.RequestOne(request, ct);
    }

    public async Task PutValue(string collection, string key, JsonNode? value, CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.PutData,
            ["collection"] = collection,
            ["key"] = key,
            ["value"] = value
        };

        await _rawClient.RequestOne(request, ct);
    }

    public async Task PutValue(string collection, string key, object value, CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.PutData,
            ["collection"] = collection,
            ["key"] = key,
            ["value"] = JsonSerializer.SerializeToNode(value)
        };

        await _rawClient.RequestOne(request, ct);
    }

    /// <summary>
    ///     Get ordered list of changes starting at an old version.
    ///     Used to synchronize two data stores
    /// </summary>
    /// <param name="version"></param>
    /// <param name="awaitIfNothingChanged">if true block (await) until data changes</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<Item> GetAllChangesSinceVersion(int version, bool awaitIfNothingChanged = false,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.StreamChanges,
            ["fromVersion"] = version,
            ["blockIfNoChange"] = awaitIfNothingChanged
        };


        await foreach (var change in _rawClient.RequestMany(request, ct).WithCancellation(ct))
        {
            var item = JsonSerializer.Deserialize(change, SerializationContext.Default.Item);
            if (item != null) yield return item;
        }
    }


    /// <summary>
    ///     For test only. Trigger an exception on the server
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task SimulateException(CancellationToken ct)
    {
        var request = new JsonObject
        {
            [RequestConstants.PropertyNameRequestType] = RequestConstants.RaiseExceptionForTests
        };

        await _rawClient.RequestOne(request, ct);
    }
}