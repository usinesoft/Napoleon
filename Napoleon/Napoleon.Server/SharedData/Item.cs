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

    public void CheckValid()
    {
        if (string.IsNullOrEmpty(Collection))
        {
            throw new FormatException("Empty collection name");
        }

        if (string.IsNullOrEmpty(Key))
        {
            throw new FormatException("Empty key name");
        }

        if (Version == 0)
        {
            throw new FormatException("Version can not be 0 on a change");
        }

        if (IsDeleted && Value.ValueKind != JsonValueKind.Undefined)
        {
            throw new FormatException("Value must be undefined on a delete change");
        }

        if (!IsDeleted && Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new FormatException("Value can be undefined only on a delete change");
        }
    }
}