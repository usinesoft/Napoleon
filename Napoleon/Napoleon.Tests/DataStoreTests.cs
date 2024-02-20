using System.Reflection;
using System.Text.Json;
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
        Assert.IsNull(value);

        // deleting an non existent value will not change the version
        var deleted = store.DeleteValue("A", "a");
        Assert.That(deleted, Is.False);
        Assert.That(store.GlobalVersion, Is.EqualTo(0));

        
        // put a simple value
        store.PutSimpleValue("config", "activate", true);
        Assert.That(store.GlobalVersion, Is.EqualTo(1));

        value = store.TryGetValue("config", "activate");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.True));

        // update the value
        store.PutSimpleValue("config", "activate", false);
        Assert.That(store.GlobalVersion, Is.EqualTo(2));

        value = store.TryGetValue("config", "activate");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.False));
        (bool bv, bool found )=  store.TryGetScalarValue<bool>("config", "activate");
        Assert.That(bv, Is.False);
        Assert.That(found, Is.True);

        // int value
        store.PutSimpleValue("config", "max_retries", 10);
        Assert.That(store.GlobalVersion, Is.EqualTo(3));

        value = store.TryGetValue("config", "max_retries");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.Number));

        // float value
        store.PutValue("config", "pi", 3.14);
        Assert.That(store.GlobalVersion, Is.EqualTo(4));

        value = store.TryGetValue("config", "pi");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.Number));

        // object value
        store.PutValue("config", "origin", new{X=1, Y=5});
        Assert.That(store.GlobalVersion, Is.EqualTo(5));

        value = store.TryGetValue("config", "origin");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.Object));

        // array value
        store.PutValue("config", "array", new[]{1,2,3});
        Assert.That(store.GlobalVersion, Is.EqualTo(6));

        value = store.TryGetValue("config", "array");
        Assert.That(value.HasValue);
        Assert.That(value!.Value.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var result = store.TryGetValue<int[]>("config", "array");
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(result, new[]{1,2,3});

        deleted = store.DeleteValue("config", "array");
        Assert.IsTrue(deleted);
        Assert.That(store.GlobalVersion, Is.EqualTo(7));

        value = store.TryGetValue("config", "array");
        Assert.IsNull(value);

        //trying to delete an already deleted value should not increment version
        deleted = store.DeleteValue("config", "array");
        Assert.IsFalse(deleted);
        Assert.That(store.GlobalVersion, Is.EqualTo(7));

        
    }

    [Test]
    public void Data_store_to_json()
    {
        var store = new DataStore();
        store.PutValue("config", "ids", new[] { 4, 15, 88 });
        store.PutValue("config", "credentials", new {Url="https://test/com/api", Token = "56465887##"});

        store.PutValue("all", "activate_ping", true);
        store.PutValue("all", "protocol", "UDP");
        store.PutValue("all", "port", 4888);
        store.DeleteValue("all", "port");

        
        var doc = store.SerializeToDocument();
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions{WriteIndented = true});

        var store2 = new DataStore();
        store2.DeserializeFromDocument(doc);
        var doc2 = store2.SerializeToDocument();
        var json2 = JsonSerializer.Serialize(doc2, new JsonSerializerOptions{WriteIndented = true});

        Assert.That(json, Is.EqualTo(json2));

        var (_, found) = store2.TryGetScalarValue<int>("all", "port");
        Assert.That(found, Is.False, "a deleted value should not be found");

        var (activatePing, found1) = store2.TryGetScalarValue<bool>("all", "activate_ping");
        Assert.That(found1, Is.True);
        Assert.That(activatePing, Is.True);
        

        var ids = store2.TryGetValue<int[]>("config", "ids");
        CollectionAssert.AreEqual(ids, new[]{4, 15, 88});

    }

    [Test]
    public void Get_changes()
    {
        var store = new DataStore();

        store.PutValue("A", "a", true); // version 1
        store.PutValue("A", "a1", true);// version 2
        store.PutValue("B", "b", "what a wonderful world"); // version 3
        store.PutValue("B", "b", "the sky is blue"); //version 4

        Assert.That(store.GlobalVersion, Is.EqualTo(4));

        var changes = store.GetChangesSince(0);
        Assert.That(changes.Count, Is.EqualTo(3));

        changes = store.GetChangesSince(2);
        Assert.That(changes.Count, Is.EqualTo(1));
        Assert.That(changes[0].Value.GetString(), Is.EqualTo("the sky is blue"));

        store.DeleteValue("B", "b");

        Assert.That(store.GlobalVersion, Is.EqualTo(5));
        changes = store.GetChangesSince(2);
        Assert.That(changes.Count, Is.EqualTo(1));
        Assert.True(changes[0].IsDeleted);
        Assert.That(changes[0].Value.ValueKind, Is.EqualTo(JsonValueKind.Undefined));



    }

}