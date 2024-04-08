using System.Text.Json.Nodes;

namespace HugeCsv.Values
{
    public interface IKeyValue
    {
        /// <summary>
        /// Type in the table schema
        /// </summary>
        KeyValueType Type { get; }
        
        /// <summary>
        /// Used for storage optimization
        /// </summary>
        bool HasDefaultValue { get; }

        JsonValue? JsonValue { get; }
    }

    public enum KeyValueType
    {
        SomeInt,
        SomeFloat,
        String,
        Date,
        Bool,
        Null
    }
}
