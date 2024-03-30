using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Moq;
using Napoleon.Server;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

namespace NetworkBenchmark;

#pragma warning disable CS8618
[MemoryDiagnoser]
public class DataRequestsTest
{
    private readonly string _complexJson = @"{
	""id"": ""0001"",
	""type"": ""donut"",
	""name"": ""Cake"",
	""ppu"": 0.55,
	""batters"":
		{
			""batter"":
				[
					{ ""id"": ""1001"", ""type"": ""Regular"" },
					{ ""id"": ""1002"", ""type"": ""Chocolate"" },
					{ ""id"": ""1003"", ""type"": ""Blueberry"" },
					{ ""id"": ""1004"", ""type"": ""Devil's Food"" }
				]
		},
	""topping"":
		[
			{ ""id"": ""5001"", ""type"": ""None"" },
			{ ""id"": ""5002"", ""type"": ""Glazed"" },
			{ ""id"": ""5005"", ""type"": ""Sugar"" },
			{ ""id"": ""5007"", ""type"": ""Powdered Sugar"" },
			{ ""id"": ""5006"", ""type"": ""Chocolate with Sprinkles"" },
			{ ""id"": ""5003"", ""type"": ""Chocolate"" },
			{ ""id"": ""5004"", ""type"": ""Maple"" }
		]
}
";

    private readonly Mock<IServer> _serverMock = new();
    private DataClient _client;

    private JsonElement _exampleData;

    private DataServer _server;


    [GlobalSetup]
    public void GlobalSetup()
    {
        var dataStore = new DataStore();

        _server = new(dataStore, _serverMock.Object);


        _server.Start(48455);


        Task.Delay(100).Wait();


        _client = new();
        _client.Connect("localhost", 48455);

        _exampleData = JsonSerializer.Deserialize<JsonElement>(_complexJson);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }


    [Benchmark]
    public async Task PutGetThenDeleteSimpleValue()
    {
        // put a new value in the data store
        await _client.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value01")!, CancellationToken.None);


        // get from the data store
        await _client.GetValue("col1", "key1", CancellationToken.None);


        // delete value
        await _client.DeleteValue("col1", "key1", CancellationToken.None);
    }

    [Benchmark]
    public async Task PutGetThenDeleteComplexValue()
    {
        // put a new value in the data store
        await _client.PutValue("col1", "key1", _exampleData, CancellationToken.None);


        // get from the data store
        await _client.GetValue("col1", "key1", CancellationToken.None);


        // delete value
        await _client.DeleteValue("col1", "key1", CancellationToken.None);
    }
}