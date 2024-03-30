using System.Text.Json;
using System.Text.Json.Nodes;

namespace Napoleon.Server.SharedData;

public partial class DataStore
{
    /// <summary>
    ///     Convert all the information contained into a single document
    /// </summary>
    /// <returns></returns>
    public JsonDocument SerializeToDocument()
    {
        var json = new JsonObject { { "GlobalVersion", GlobalVersion } };

        lock (_sync)
        {
            foreach (var collectionContent in Items.Values.GroupBy(x => x.Collection))
            {
                var collection = new JsonObject();
                foreach (var item in collectionContent.OrderBy(x => x.Key))
                {
                    var versioned = new JsonObject
                    {
                        { "version", item.Version }
                    };

                    var asNode = item.IsDeleted ? null : item.Value.Deserialize(SerializationContext.Default.JsonNode);
                    if (asNode != null) versioned.Add("value", asNode);

                    collection.Add(item.Key!, versioned);
                }

                json.Add(collectionContent.Key!, collection);
            }
        }


        return JsonSerializer.SerializeToDocument(json, SerializationContext.Default.JsonObject);
    }

    /// <summary>
    ///     Initialize from json document
    /// </summary>
    /// <param name="jDoc"></param>
    /// <exception cref="FormatException"></exception>
    public void DeserializeFromDocument(JsonDocument jDoc)
    {
        lock (_sync)
        {
            // reset the content
            GlobalVersion = 0;
            Items.Clear();

            foreach (var itemLevel0 in jDoc.RootElement.EnumerateObject())
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
                            throw new FormatException("Invalid data store json");

                        var version = valueAndVersion[0].Value.GetInt32();

                        JsonElement? value = null;
                        if (valueAndVersion.Length == 2 && valueAndVersion[1].Name == "value")
                            value = valueAndVersion[1].Value;

                        var k = new GlobalKey(collectionName, keyName);
                        Items[k] = new()
                        {
                            Collection = collectionName,
                            Value = value ?? default,
                            Version = version,
                            IsDeleted = !value.HasValue,
                            Key = keyName
                        };
                    }
                }

            CheckConsistency();
        }
    }
}