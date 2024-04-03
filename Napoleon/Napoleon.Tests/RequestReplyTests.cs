using System.Text.Json;
using Moq;
using Napoleon.Server;
using Napoleon.Server.RequestReply;
using Napoleon.Server.SharedData;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Napoleon.Tests;

public class RequestReplyTests
{
    private readonly Mock<IServer> _serverMock = new();


    [Test]
    public async Task Client_writes_and_reads_data_with_tcp()
    {
        _serverMock.Setup(s => s.MyStatus).Returns(StatusInCluster.Leader);


        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);


        var port = dataServer.Start(0);


        await Task.Delay(100);


        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);

        // put a new value in the data store
        await dataClient.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value01")!, CancellationToken.None);

        // check it is in the data store
        var fromDataStore = dataStore.TryGetValue("col1", "key1");
        Assert.That(fromDataStore.ToString(), Is.EqualTo("value01"));

        // get from the data store
        var reloaded = await dataClient.GetValue("col1", "key1", CancellationToken.None);
        Assert.That(reloaded.ToString(), Is.EqualTo("value01"));

        // delete value
        var wasDeleted = await dataClient.DeleteValue("col1", "key1", CancellationToken.None);
        Assert.That(wasDeleted.ValueKind, Is.EqualTo(JsonValueKind.True));

        // delete again will return false
        wasDeleted = await dataClient.DeleteValue("col1", "key1", CancellationToken.None);
        Assert.That(wasDeleted.ValueKind, Is.EqualTo(JsonValueKind.False));

        // trying to read a deleted value returns undefined
        reloaded = await dataClient.GetValue("col1", "key1", CancellationToken.None);
        Assert.That(reloaded.ValueKind, Is.EqualTo(JsonValueKind.Undefined));


        await Task.Delay(100);
    }


    [Test]
    public async Task Trying_to_write_data_on_a_server_which_is_not_leader_fails()
    {
        _serverMock.Setup(s => s.MyStatus).Returns(StatusInCluster.Follower);


        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);


        var port = dataServer.Start(0);


        await Task.Delay(100);


        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);

        // add value should fail
        Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await dataClient.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value01")!,
                CancellationToken.None);
        });

        // update a value should fail
        Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await dataClient.PutValue("col1", "key1", JsonSerializer.SerializeToNode("value02")!,
                CancellationToken.None);
        });

        // delete a value should fail
        Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await dataClient.DeleteValue("col1", "key1", CancellationToken.None);
        });
    }

    [Test]
    public async Task Simulate_exception_on_the_server()
    {
        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);

        var port = dataServer.Start(0);
        await Task.Delay(100);


        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);


        try
        {
            await dataClient.SimulateException(CancellationToken.None);

            Assert.Fail("An exception should have been raised");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [Test]
    public async Task Synchronize_data_stores_with_tcp_when_a_single_key_changed()
    {
        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);

        var port = dataServer.Start(0);
        await Task.Delay(100);

        // make changes into the data store
        for (var i = 0; i < 100; i++) dataStore.PutSimpleValue("collection1", "key001", i);


        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);

        var clientData = new DataStore();
        await foreach (var change in dataClient.GetAllChangesSinceVersion(0)) clientData.ApplyChanges(new[] { change });

        Assert.IsTrue(DataStoreTests.DataStoresAreIdentical(dataStore, clientData));
    }

    [Test]
    public async Task Synchronize_data_stores_with_tcp_when_values_have_been_deleted()
    {
        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);

        var port = dataServer.Start(0);

        await Task.Delay(100);
        dataStore.PutValue("coll01", "key01", 45);
        dataStore.PutValue("coll01", "key02", 46);
        dataStore.DeleteValue("coll01", "key01");

        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);

        var clientData = new DataStore();
        await foreach (var change in dataClient.GetAllChangesSinceVersion(0)) clientData.ApplyChanges(new[] { change });

        Assert.IsTrue(DataStoreTests.DataStoresAreIdentical(dataStore, clientData));

        // synchronize another data store using the same client
        var clientData1 = new DataStore();
        await foreach (var change in dataClient.GetAllChangesSinceVersion(0))
            clientData1.ApplyChanges(new[] { change });

        Assert.IsTrue(DataStoreTests.DataStoresAreIdentical(dataStore, clientData1));

        Assert.IsTrue(DataStoreTests.DataStoresAreIdentical(dataStore, clientData));
    }

    [Test]
    public async Task Synchronize_data_stores_with_tcp_when_multiple_keys_changed()
    {
        var dataStore = new DataStore();

        using var dataServer = new DataServer(dataStore, _serverMock.Object);

        var port = dataServer.Start(0);
        await Task.Delay(100);

        // make changes to the data store
        for (var i = 0; i < 100; i++) dataStore.PutSimpleValue("collection1", $"key{i}", i);

        dataStore.DeleteValue("collection1", "key44");


        using var dataClient = new DataClient();
        dataClient.Connect("localhost", port);

        var clientData = new DataStore();
        await foreach (var change in dataClient.GetAllChangesSinceVersion(0)) clientData.ApplyChanges(new[] { change });

        Assert.IsTrue(DataStoreTests.DataStoresAreIdentical(dataStore, clientData));
    }
}