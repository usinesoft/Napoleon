using System.Text.Json.Nodes;

namespace HugeCsv.Values;

public sealed class NullValue:IKeyValue
{
    public KeyValueType Type => KeyValueType.Null;
    public bool HasDefaultValue => true;
    public JsonValue? JsonValue => null;
}