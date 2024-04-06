using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Napoleon.Server.Messages;
using Napoleon.Server.SharedData;

namespace Napoleon.Tests;

public class DataStoreTests
{
    [Test]
    public void Add_update_delete_values_in_the_data_store()
    {
        // create a new empty one
        var store = new DataStore();

        Assert.That(store.GlobalVersion, Is.EqualTo(0));


        var value = store.TryGetValue("config", "cx");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Undefined));

        // deleting an non existent value will not change the version
        var deleted = store.DeleteValue("A", "a");
        Assert.That(deleted, Is.False);
        Assert.That(store.GlobalVersion, Is.EqualTo(0));


        // put a simple value
        store.PutSimpleValue("config", "activate", true);
        Assert.That(store.GlobalVersion, Is.EqualTo(1));

        value = store.TryGetValue("config", "activate");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.True));

        // update the value
        store.PutSimpleValue("config", "activate", false);
        Assert.That(store.GlobalVersion, Is.EqualTo(2));

        value = store.TryGetValue("config", "activate");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.False));
        (var bv, var found) = store.TryGetScalarValue<bool>("config", "activate");
        Assert.That(bv, Is.False);
        Assert.That(found, Is.True);

        // int value
        store.PutSimpleValue("config", "max_retries", 10);
        Assert.That(store.GlobalVersion, Is.EqualTo(3));

        value = store.TryGetValue("config", "max_retries");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Number));

        // float value
        store.PutValue("config", "pi", 3.14);
        Assert.That(store.GlobalVersion, Is.EqualTo(4));

        value = store.TryGetValue("config", "pi");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Number));

        // object value
        store.PutValue("config", "origin", new { X = 1, Y = 5 });
        Assert.That(store.GlobalVersion, Is.EqualTo(5));

        value = store.TryGetValue("config", "origin");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Object));

        // array value
        store.PutValue("config", "array", new[] { 1, 2, 3 });
        Assert.That(store.GlobalVersion, Is.EqualTo(6));

        value = store.TryGetValue("config", "array");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var result = store.TryGetValue<int[]>("config", "array");
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(result, new[] { 1, 2, 3 });

        deleted = store.DeleteValue("config", "array");
        Assert.IsTrue(deleted);
        Assert.That(store.GlobalVersion, Is.EqualTo(7));

        value = store.TryGetValue("config", "array");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Undefined));

        //trying to delete an already deleted value should not increment version
        deleted = store.DeleteValue("config", "array");
        Assert.IsFalse(deleted);
        Assert.That(store.GlobalVersion, Is.EqualTo(7));

        // string values can be simple strings or contain Json
        store.PutSimpleValue("config", "name", "foo");
        Assert.That(store.GlobalVersion, Is.EqualTo(8));

        value = store.TryGetValue("config", "name");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.String));

        // putting a json string will store an object
        store.PutSimpleValue("config", "object", "{\"foo\":\"bar\"}");
        Assert.That(store.GlobalVersion, Is.EqualTo(9));

        value = store.TryGetValue("config", "object");
        Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.Object));
    }

    [Test]
    public void Data_store_to_json()
    {
        var store = new DataStore();
        store.PutValue("config", "ids", new[] { 4, 15, 88 });
        store.PutValue("config", "credentials", new { Url = "https://test/com/api", Token = "56465887##" });

        store.PutValue("all", "activate_ping", true);
        store.PutValue("all", "protocol", "UDP");
        store.PutValue("all", "port", 4888);
        store.DeleteValue("all", "port");


        var doc = store.SerializeToDocument();
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });

        var store2 = new DataStore();
        store2.DeserializeFromDocument(doc);
        var doc2 = store2.SerializeToDocument();
        var json2 = JsonSerializer.Serialize(doc2, new JsonSerializerOptions { WriteIndented = true });

        Assert.That(json, Is.EqualTo(json2));

        var (_, found) = store2.TryGetScalarValue<int>("all", "port");
        Assert.That(found, Is.False, "a deleted value should not be found");

        var (activatePing, found1) = store2.TryGetScalarValue<bool>("all", "activate_ping");
        Assert.That(found1, Is.True);
        Assert.That(activatePing, Is.True);


        var ids = store2.TryGetValue<int[]>("config", "ids");
        CollectionAssert.AreEqual(ids, new[] { 4, 15, 88 });
    }

    [Test]
    public void Get_changes()
    {
        var store = new DataStore();

        store.PutValue("A", "a", true); // version 1
        store.PutValue("A", "a1", true); // version 2
        store.PutValue("B", "b", "what a wonderful world"); // version 3
        store.PutValue("B", "b", "the sky is blue"); //version 4

        Assert.That(store.GlobalVersion, Is.EqualTo(4));

        var changes = store.GetChangesSince(0);
        Assert.That(changes.Count, Is.EqualTo(3));

        changes = store.GetChangesSince(2);
        Assert.That(changes.Count, Is.EqualTo(1));
        Assert.That(changes[0].Value.GetString(), Is.EqualTo("the sky is blue"));

        store.DeleteValue("B", "b"); // version 5

        Assert.That(store.GlobalVersion, Is.EqualTo(5));
        changes = store.GetChangesSince(2);
        Assert.That(changes.Count, Is.EqualTo(1));
        Assert.True(changes[0].IsDeleted);
    }

    public static bool DataStoresAreIdentical(DataStore store1, DataStore store2)
    {
        var doc1 = store1.SerializeToDocument();
        var json1 = JsonSerializer.Serialize(doc1, new JsonSerializerOptions { WriteIndented = true });

        var doc2 = store2.SerializeToDocument();
        var json2 = JsonSerializer.Serialize(doc2, new JsonSerializerOptions { WriteIndented = true });

        return json1 == json2;
    }

    [Test]
    public void Data_stores_synchronization()
    {
        var masterData = new DataStore();

        var followerData = new DataStore();

        Assert.That(DataStoresAreIdentical(masterData, followerData));

        // apply the same change to both
        var change1 = new Item
            { Collection = "A", Key = "a", Version = 1, Value = JsonSerializer.SerializeToElement(15) };
        var applied1ToMaster = masterData.TryApplyAsyncChange(change1);
        Assert.That(applied1ToMaster);
        var applied1ToFollower = followerData.TryApplyAsyncChange(change1);
        Assert.That(applied1ToFollower);

        Assert.That(DataStoresAreIdentical(masterData, followerData));

        // apply a new change only to master
        var change2 = new Item
            { Collection = "A", Key = "a", Version = 2, Value = JsonSerializer.SerializeToElement(16) };
        var applied2ToMaster = masterData.TryApplyAsyncChange(change2);
        Assert.That(applied2ToMaster);
        Assert.That(!DataStoresAreIdentical(masterData, followerData));
        Assert.That(masterData.GlobalVersion, Is.EqualTo(2));
        Assert.That(followerData.GlobalVersion, Is.EqualTo(1));

        // apply again a new change only to master
        var change3 = new Item
            { Collection = "A", Key = "a", Version = 3, Value = JsonSerializer.SerializeToElement(17) };
        var applied3ToMaster = masterData.TryApplyAsyncChange(change3);
        Assert.That(applied3ToMaster);
        Assert.That(!DataStoresAreIdentical(masterData, followerData));
        Assert.That(masterData.GlobalVersion, Is.EqualTo(3));
        Assert.That(followerData.GlobalVersion, Is.EqualTo(1));

        // try to apply change3 to the follower. That should fail as it is out of order
        // In real life that can happen if a messaged was missed or received out of order
        var applied3ToFollower = followerData.TryApplyAsyncChange(change3);
        Assert.False(applied3ToFollower);
        Assert.That(followerData.GlobalVersion, Is.EqualTo(1), "No version change if failed to apply change");

        // now get the full sequence of changes from the master and apply changes.
        var changes = masterData.GetChangesSince(followerData.GlobalVersion);
        followerData.ApplyChanges(changes);
        Assert.That(DataStoresAreIdentical(masterData, followerData));

        // fully synchronize an empty data store using messages
        var newcomer = new DataStore();
        var all = followerData.GetChangesSince(0); // all data

        var syncMessage = MessageHelper.CreateDataSyncMessage("tst", "node01", all);
        Assert.That(syncMessage.MessageType, Is.EqualTo(MessageType.DataSync));
        var changesFromMessage = syncMessage.FromMessage().Items;
        newcomer.ApplyChanges(changesFromMessage);

        Assert.That(DataStoresAreIdentical(newcomer, followerData));
    }

    
    [Test]
    public void Store_restore_changes_with_persistence_engine()
    {
        var store = new DataStore();

        store.PutValue("A", "a", true); // version 1
        store.PutValue("A", "a1", true); // version 2
        store.PutValue("B", "b", "what a wonderful world"); // version 3
        store.PutValue("B", "b", "the sky is blue"); //version 4

        store.DeleteValue("B", "b"); // version 5

        Assert.That(store.GlobalVersion, Is.EqualTo(5));

        const string dataDirectory = "test_data";
        
        // reset the data directory
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, true);
        }
        
        Directory.CreateDirectory(dataDirectory);
        

        var persistenceEngine = new PersistenceEngine(new NullLogger<PersistenceEngine>());

        persistenceEngine.SaveData(store, dataDirectory);

        var newStore = new DataStore();
        persistenceEngine.LoadData(newStore, dataDirectory);
        Assert.That(newStore.GlobalVersion, Is.EqualTo(5));

        var (bValue, found) = newStore.TryGetScalarValue<bool>("A", "a1");

        Assert.That(found, Is.True);
        Assert.That(bValue, Is.True);

        var deletedValue = newStore.TryGetValue<string>("B", "b");
        Assert.That(deletedValue, Is.Null);

        var newVersion = newStore.GlobalVersion+1;

        //save a change then reload 
        persistenceEngine.SaveChange(new Item{Collection = "A", Key = "a1", IsDeleted = true, Version = newVersion, Value = JsonDocument.Parse("null").RootElement}, dataDirectory);

        newStore = new DataStore();
        persistenceEngine.LoadData(newStore, dataDirectory);

        (_, found) = newStore.TryGetScalarValue<bool>("A", "a1");

        Assert.That(found, Is.False);
        Assert.That(newStore.GlobalVersion, Is.EqualTo(newVersion));

    }
}