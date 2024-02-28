using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace NetworkBenchmark;

#pragma warning disable CS8618
[MemoryDiagnoser]
public class DataRequestsTest
{
    private DataServer _server;
    private DataClient _client;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var dataStore = new DataStore();

        _server = new DataServer(dataStore);


        _server.Start(48455);


        Task.Delay(100).Wait();
                

        _client = new DataClient("localhost", 48455);
        _client.Connect();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    

    [Benchmark]
    public async Task PutGetThenDelete()
    {
        // put a new value in the data store
        await _client.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value01")!, CancellationToken.None);

        
        // get from the data store
        await _client.GetValue("col1", "key1", CancellationToken.None);
        

        // delete value
        await _client.DeleteValue("col1", "key1", CancellationToken.None);
        

    }

    
        
}