using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace NetworkBenchmark;

#pragma warning disable CS8618
[MemoryDiagnoser]
public class DataSynchronizationTest
{
    private DataClient _client;

    private DataServer _server;


    [Params(10, 100, 1000)]
    // ReSharper disable once MemberCanBePrivate.Global
    public int ChangeCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var dataStore = new DataStore();

        // apply 1000 changes to the data store
        for (var i = 0; i < 1000; i++) dataStore.PutValue("test", $"key{i:D5}", i);

        _server = new(dataStore, new NullLogger<DataServer>());


        _server.Start(48555);


        Task.Delay(100).Wait();


        _client = new();
        _client.Connect("localhost", 48555);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark]
    public async Task DataSynchronization()
    {
        var clientDataStore = new DataStore();


        await foreach (var change in _client.GetAllChangesSinceVersion(1000 - ChangeCount))
            clientDataStore.ApplyChanges(new[] { change });
    }

    /// <summary>
    ///     Used to run under debugger
    /// </summary>
    /// <returns></returns>
    public async Task Debug()
    {
        ChangeCount = 10;
        GlobalSetup();
        await DataSynchronization();
        await DataSynchronization();
    }
}