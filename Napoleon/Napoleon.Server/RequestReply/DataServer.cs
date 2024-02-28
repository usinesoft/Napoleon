using System.Text.Json;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.RequestReply;


/// <summary>
/// Handles all data requests
/// </summary>
public sealed class DataServer:IDisposable
{
    private readonly RawServer _rawServer = new();

    private readonly DataStore _dataStore;

    JsonElement DataReadHandler(JsonDocument request)
    {
        
        var collection = request.RootElement.GetProperty("collection").GetString() ?? throw new NotSupportedException("key is not specified");
        var key = request.RootElement.GetProperty("key").GetString() ?? throw new NotSupportedException("key is not specified");

        return _dataStore.TryGetValue(collection, key);
        
    }

    JsonElement DataDeleteHandler(JsonDocument request)
    {
        
        var collection = request.RootElement.GetProperty("collection").GetString() ?? throw new NotSupportedException("key is not specified");
        var key = request.RootElement.GetProperty("key").GetString() ?? throw new NotSupportedException("key is not specified");
        
        var deleted = _dataStore.DeleteValue(collection, key);

        return JsonSerializer.SerializeToElement(deleted);

    }

    JsonElement DataWriteHandler(JsonDocument request) 
    {
        
        var collection = request.RootElement.GetProperty("collection").GetString() ?? throw new NotSupportedException("key is not specified");
        var key = request.RootElement.GetProperty("key").GetString() ?? throw new NotSupportedException("key is not specified");
        var value = request.RootElement.GetProperty("value");

        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("The request should contain a 'value' property");
        }

        _dataStore.PutValue(collection, key, value);

        return default; // void response


    }

    public DataServer(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    
    public Task Start(int port)
    {
        _rawServer.RegisterRequestHandler(RequestConstants.GetData, DataReadHandler);
        _rawServer.RegisterRequestHandler(RequestConstants.PutData, DataWriteHandler);
        _rawServer.RegisterRequestHandler(RequestConstants.DeleteData, DataDeleteHandler);

        _rawServer.RegisterRequestHandler(RequestConstants.RaiseExceptionForTests, _=> throw new ApplicationException("Test exception (you requested it)"));

        return _rawServer.Start(port);
    }

    public void Dispose()
    {
        _rawServer.Dispose();
    }
}