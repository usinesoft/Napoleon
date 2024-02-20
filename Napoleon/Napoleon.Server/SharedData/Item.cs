using System.Text.Json;
using System.Xml.Schema;

namespace Napoleon.Server.SharedData;

/// <summary>
/// A unit of data in the data store
/// </summary>
public class Item
{
    /// <summary>
    /// The whole data store has a global version that is used for synchronization.
    /// Each item stores the global version when it was last modified (created/updated/or deleted)
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Items are organized in collections
    /// </summary>
    public string? Collection { get; set; }

    /// <summary>
    /// Unique key inside a collection
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Value as read-only json
    /// </summary>
    public JsonElement Value { get; set; }

    /// <summary>
    /// Items are logically deleted to keep the version for synchronization
    /// The <see cref="Value"/> is deleted physically to release memory
    /// </summary>
    public bool IsDeleted { get; set; }

    public Item Clone()
    {
        return new Item
        {
            Value = Value.ValueKind == JsonValueKind.Undefined?  default : Value.Clone() , // undefined is not cloneable
            Collection = Collection,
            IsDeleted = IsDeleted,
            Key = Key,
            Version = Version
        };
    }
}