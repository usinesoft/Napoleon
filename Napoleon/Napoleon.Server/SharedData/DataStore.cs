using System.Text.Json;
using System.Text.Json.Nodes;

namespace Napoleon.Server.SharedData;

public class DataStore
{

    private readonly object _sync = new();

    /// <summary>
    /// Each modification of an item in the store increments this value
    /// </summary>
    public int GlobalVersion { get; private set; }

    /// <summary>
    /// Indexed data
    /// </summary>
    private Dictionary<GlobalKey, Item> Items { get; } = new();


    /// <summary>
    /// Convert all the information contained into a single document
    /// </summary>
    /// <returns></returns>
    public JsonDocument SerializeToDocument()
    {
        JsonObject json = new JsonObject { { "GlobalVersion", GlobalVersion } };

        foreach (var collectionContent in Items.Values.GroupBy(x=>x.Collection))
        {
            var collection = new JsonObject();
            foreach (var item in collectionContent)
            {
                
                var versioned = new JsonObject
                {
                    { "version", item.Version }
                };

                var asNode = item.IsDeleted? null: item.Value.Deserialize<JsonNode>();
                if (asNode != null)
                {
                    versioned.Add("value", asNode);
                }

                collection.Add(item.Key!, versioned);

            }

            json.Add(collectionContent.Key!, collection);
        }


        return JsonSerializer.SerializeToDocument(json);
    }

    public void DeserializeFromDocument(JsonDocument jDoc)
    {
        // reset the content
        GlobalVersion = 0;
        Items.Clear();

        foreach (var itemLevel0 in jDoc.RootElement.EnumerateObject())
        {
            if (itemLevel0.Name == "GlobalVersion")
            {
                GlobalVersion = itemLevel0.Value.GetInt32();
            }
            else // a collection name
            {
                var collectionName = itemLevel0.Name;
                
                
                var collection = itemLevel0.Value;
                foreach (var kv in collection.EnumerateObject())
                {
                    var keyName = kv.Name;
                    var valueAndVersion = kv.Value.EnumerateObject().ToArray();
                    if (valueAndVersion.Length < 1 || valueAndVersion[0].Name != "version")
                    {
                        throw new FormatException("Invalid data store json");
                    }
                    var version = valueAndVersion[0].Value.GetInt32();

                    JsonElement? value = null;
                    if (valueAndVersion.Length == 2 && valueAndVersion[1].Name == "value")
                    {
                        value = valueAndVersion[1].Value;
                    }

                    var k = new GlobalKey(collectionName, keyName);
                    Items[k] = new Item
                    {
                        Collection = collectionName,
                        Value = value ?? default,
                        Version = version,
                        IsDeleted = !value.HasValue,
                        Key = keyName
                    };
                }

            }
        }

        
    }

    public void PutSimpleValue(string collection, string key, string value)
    {
        var jsonValue = JsonDocument.Parse($"\"{value}\"").RootElement;

        PutValue(collection, key, jsonValue);
    }

    public void PutSimpleValue(string collection, string key, bool value)
    {
        var jsonValue = JsonDocument.Parse(value ? "true" : "false").RootElement;

        PutValue(collection, key, jsonValue);
    }

    public void PutSimpleValue(string collection, string key, int value)
    {
        var jsonValue = JsonDocument.Parse($"{value}").RootElement;

        PutValue(collection, key, jsonValue);
    }

    public void PutValue(string collection, string key, object value)
    {
        var jsonValue = JsonSerializer.SerializeToElement(value);

        PutValue(collection, key, jsonValue);
    }


    public void PutValue(string collection, string key, JsonElement value)
    {

        lock (_sync)
        {
            GlobalVersion++;

            var k = new GlobalKey(collection, key);

            var item = new Item
            {
                Collection = collection,
                Key = key,
                Value = value.Clone(),
                Version = GlobalVersion
            };

            Items[k] = item;
        }
    }


    /// <summary>
    /// Get as json
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public JsonElement? TryGetValue(string collection, string key)
    {
        var k = new GlobalKey(collection, key);
        lock (_sync)
        {
            if (Items.TryGetValue(k, out var item) && !item.IsDeleted)
            {
                return item.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Get as typed object
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public T? TryGetValue<T>(string collection, string key) where T : class
    {
        var k = new GlobalKey(collection, key);
        lock (_sync)
        {
            if (Items.TryGetValue(k, out var item) && !item.IsDeleted)
            {
                return item.Value.Deserialize<T>();
            }
        }

        return null;
    }


    /// <summary>
    /// Return a typed simple value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public Tuple<T, bool> TryGetScalarValue<T>(string collection, string key) where T : struct
    {
        var k = new GlobalKey(collection, key);


        lock (_sync)
        {
            if (!Items.TryGetValue(k, out var item) || item.IsDeleted) return new(default, false);
        
            var value = item.Value.Deserialize<T>();
            return new(value, true);
        }

        
    }

    public bool DeleteValue(string collection, string key)
    {

        var k = new GlobalKey(collection, key);

        lock (_sync)
        {
            if (!Items.TryGetValue(k, out var item) || item.IsDeleted) return false;

            // only increment if a change was made
            GlobalVersion++;

            item.IsDeleted = true;
            item.Value = default;
            item.Version = GlobalVersion;

            return true;
        }

    }

}