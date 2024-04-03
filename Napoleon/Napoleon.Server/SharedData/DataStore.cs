using System.Text.Json;

namespace Napoleon.Server.SharedData;

public partial class DataStore : IDataStore, IReadOnlyDataStore
{
    private readonly object _sync = new();

    /// <summary>
    ///     Indexed data
    /// </summary>
    private Dictionary<GlobalKey, Item> Items { get; } = new();


    /// <summary>
    ///     Each modification of an item in the store increments this value
    /// </summary>
    public int GlobalVersion { get; private set; }


    /// <summary>
    ///     Individual changes can be applied only in order to guarantee data consistency.
    ///     They are usually received through an async channel that does not guarantee that the messages are delivered in-order
    /// </summary>
    /// <param name="change"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool TryApplyAsyncChange(Item change)
    {
        if (change == null) throw new ArgumentNullException(nameof(change));
        change.CheckValid();

        lock (_sync)
        {
            if (GlobalVersion != change.Version - 1) // strong constraint
                return false;

            Items[new(change.Collection!, change.Key!)] = change;
            GlobalVersion = change.Version;
        }

        return true;
    }


    /// <summary>
    ///     Apply an ordered sequence of changes. It comes through a channel that guarantees ordered delivery
    /// </summary>
    /// <param name="changes"></param>
    public void ApplyChanges(IEnumerable<Item> changes)
    {
        lock (_sync)
        {
            foreach (var change in changes)
            {
                change.CheckValid();

                if (change.Version <= GlobalVersion)
                    throw new NotSupportedException(
                        $"An ordered sequence of changes has been received that is inconsistent with the data-store version.\nReceived change:{change} MyVersion = {GlobalVersion}");

                Items[new(change.Collection!, change.Key!)] = change;
                GlobalVersion = change.Version;
            }
        }
    }

    public event EventHandler<DataChangedEventArgs>? AfterDataChanged;


    /// <summary>
    ///     Returns the value as <see cref="JsonElement" />. If the value is not fount the returned JSonElement has ValueKind =
    ///     Undefined
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public JsonElement TryGetValue(string collection, string key)
    {
        var k = new GlobalKey(collection, key);
        lock (_sync)
        {
            if (Items.TryGetValue(k, out var item) && !item.IsDeleted) return item.Value;
        }

        return default;
    }

    /// <summary>
    ///     Get as typed object
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public T? TryGetValue<T>(string collection, string key) where T : class
    {
        var k = new GlobalKey(collection, key);
        lock (_sync)
        {
            if (Items.TryGetValue(k, out var item) && !item.IsDeleted) return item.Value.Deserialize<T>();
        }

        return null;
    }


    /// <summary>
    ///     Return a typed simple value
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


    /// <summary>
    ///     Verify that the global version is present on a key/value (and exactly one).
    ///     Every key/value should have a unique version
    /// </summary>
    private void CheckConsistency()
    {
        var maxVersion = 0;
        HashSet<int> versionsOnKeys = new();
        foreach (var item in Items)
        {
            var version = item.Value.Version;
            var newOne = versionsOnKeys.Add(version);
            if (!newOne) throw new FormatException($"The version {version} found more than once.");

            if (version > maxVersion) maxVersion = version;
        }

        if (GlobalVersion != maxVersion)
            throw new FormatException(
                $"Global version {GlobalVersion} different from the most recent version of a key/value {maxVersion}.");
    }

    public void PutSimpleValue(string collection, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));

        // value can be a json document or a simple string
        value = value.Trim();
        if (!value.StartsWith('{')) value = $"\"{value}\"";
        var jsonValue = JsonDocument.Parse(value).RootElement;

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
        Item changedItem;

        var k = new GlobalKey(collection, key);

        var item = new Item
        {
            Collection = collection,
            Key = key,
            Value = value.Clone(),
            Version = GlobalVersion + 1
        };

        BeforeDataChanged?.Invoke(this, new(item));

        lock (_sync)
        {
            GlobalVersion++;


            Items[k] = item;

            changedItem = item;
        }

        AfterDataChanged?.Invoke(this, new(changedItem));
    }

    public bool DeleteValue(string collection, string key)
    {
        var k = new GlobalKey(collection, key);

        Item? changedItem;

        lock (_sync)
        {
            if (!Items.TryGetValue(k, out var item) || item.IsDeleted) return false;

            // only increment if a change was made
            GlobalVersion++;

            item.IsDeleted = true;
            item.Value = JsonDocument.Parse("null").RootElement;
            item.Version = GlobalVersion;

            changedItem = item;
        }

        AfterDataChanged?.Invoke(this, new(changedItem));


        return true;
    }

    public IList<Item> GetChangesSince(int baseVersion)
    {
        if (baseVersion > GlobalVersion)
            throw new ArgumentException(
                $"Can not get changes: last version {GlobalVersion} is less than the base version {baseVersion}");

        var changes = new List<Item>();

        // do not work too hard if nothing changed
        if (baseVersion == GlobalVersion) return changes;


        lock (_sync)
        {
            foreach (var item in Items.Select(x => x.Value))
                if (item.Version > baseVersion)
                    changes.Add(item.Clone());
        }

        return changes.OrderBy(x => x.Version).ToList();
    }

    public event EventHandler<DataChangedEventArgs>? BeforeDataChanged;
}