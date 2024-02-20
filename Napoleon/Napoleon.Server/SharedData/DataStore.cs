using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Napoleon.Server.SharedData;

public partial class DataStore
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
    /// Verify that the global version is present on a key/value (and exactly one).
    /// Every key/value should have a unique version
    /// </summary>
    private void CheckConsistency()
    {
        var maxVersion = 0;
        HashSet<int> versionsOnKeys = new();
        foreach (var item in Items)
        {
            var version = item.Value.Version;
            var newOne = versionsOnKeys.Add(version);
            if (!newOne)
            {
                throw new FormatException($"The version {version} found more than once.");
            }

            if (version > maxVersion)
            {
                maxVersion = version;
            }
        }

        if (GlobalVersion != maxVersion)
        {
            throw new FormatException(
                $"Global version {GlobalVersion} different from the most recent version of a key/value {maxVersion}.");
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

    public IList<Item> GetChangesSince(int baseVersion)
    {
        if (baseVersion >= GlobalVersion)
        {
            throw new ArgumentException($"Can not get changes: last version {GlobalVersion} is less than the base version {baseVersion}");
        }

        var changes = new List<Item>();

        lock (_sync)
        {
            foreach (var item in Items.Select(x=> x.Value))
            {
                if (item.Version > baseVersion)
                {
                    changes.Add(item.Clone());
                }
            }
        }

        return changes.OrderBy(x=>x.Version).ToList();
    }
}
