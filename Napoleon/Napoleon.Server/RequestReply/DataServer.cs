using System.Text.Json;
using Napoleon.Server.Messages;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.RequestReply;

/// <summary>
///     TCP server that handles:
///     - Requests from clients (data sync, cluster status)
///     - Data synchronization requests from other servers
/// </summary>
public sealed class DataServer : IDisposable
{
    private readonly DataStore _dataStore;
    private readonly RawServer _rawServer = new();

    private readonly IServer _server;

    public DataServer(DataStore dataStore, IServer server)
    {
        _dataStore = dataStore;
        _server = server;
    }

    public void Dispose()
    {
        _rawServer.Dispose();
    }


    /// <summary>
    ///     Return the value of a key in a collection or null if not found
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private Task<string?> DataReadHandler(JsonDocument request)
    {
        var collection = request.GetString("collection");
        var key = request.GetString("key");

        var value = _dataStore.TryGetValue(collection, key);

        if (value.ValueKind == JsonValueKind.Undefined)
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(JsonSerializer.Serialize(value, SerializationContext.Default.JsonElement));
    }

    /// <summary>
    ///     Precondition for data altering operations
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    private void RequireLeader()
    {
        if (_server.MyStatus != StatusInCluster.Leader)
            throw new NotSupportedException(
                $"A request to change data was received by a node which is not the leader. Status = {_server.MyStatus}");
    }

    /// <summary>
    ///     Delete a key from a collection. Returns false if not found
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private Task<string?> DataDeleteHandler(JsonDocument request)
    {
        RequireLeader();

        var collection = request.GetString("collection");
        var key = request.GetString("key");

        var deleted = _dataStore.DeleteValue(collection, key);

        return Task.FromResult<string?>(deleted ? "true" : "false");
    }


    /// <summary>
    ///     Add a key/value or update the value of a key
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private Task<string?> DataWriteHandler(JsonDocument request)
    {
        RequireLeader();


        var collection = request.GetString("collection");
        var key = request.GetString("key");
        var value = request.GetValue("value");


        _dataStore.PutValue(collection, key, value);

        return Task.FromResult<string?>(null); // void response
    }

    /// <summary>
    ///     Return a list of nodes in the cluster with their information
    /// </summary>
    /// <param name="_"></param>
    /// <returns></returns>
    private Task<string?> ServerStatusHandler(JsonDocument _)
    {
        var status = _server.AllNodes();

        return Task.FromResult<string?>(JsonSerializer.Serialize(status,
            SerializationContext.Default.NodeStatusArray));
    }


    /// <summary>
    ///     Used for data synchronization. Get all the changes between two versions.
    ///     Two modes:
    ///     - return the ordered list of changes even if empty
    ///     - return non empty list of changes or await for data to be changed (the caller is already synchronized)
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private async IAsyncEnumerable<string> GetChangesHandler(JsonDocument request)
    {
        var startVersion = request.GetInt("fromVersion");

        var blockIfNoChange = request.GetBool("blockIfNoChange", false);

        await _server.WaitSyncingEnd(); // in case the server is in the middle of a synchronization operation

        var changes = _dataStore.GetChangesSince(startVersion);

        // in this case the client wants to be answered only when data changes
        if (changes.Count == 0 && blockIfNoChange)
        {
            var wakeUp = new WakeUpCall();
            _server.WakeMeUpWhenDataChanged(wakeUp);

            await wakeUp.WaitForCall();
            changes = _dataStore.GetChangesSince(startVersion);
        }


        foreach (var change in changes)
            yield return JsonSerializer.Serialize(change, SerializationContext.Default.Item);
    }


    /// <summary>
    ///     Start the server (non blocking)
    /// </summary>
    /// <param name="port">A specific port or 0 if a free port will be selected automatically</param>
    /// <exception cref="ApplicationException"></exception>
    public int Start(int port)
    {
        _rawServer.RegisterRequestHandler(RequestConstants.GetData, DataReadHandler);
        _rawServer.RegisterRequestHandler(RequestConstants.PutData, DataWriteHandler);
        _rawServer.RegisterRequestHandler(RequestConstants.DeleteData, DataDeleteHandler);
        _rawServer.RegisterRequestHandler(RequestConstants.GetClusterStatus, ServerStatusHandler);
        _rawServer.RegisterStreamingRequestHandler(RequestConstants.StreamChanges, GetChangesHandler);

        // this one is for testing only
        _rawServer.RegisterRequestHandler(RequestConstants.RaiseExceptionForTests,
            _ => throw new ApplicationException("Test exception (you requested it)"));

        return _rawServer.Start(port);
    }
}