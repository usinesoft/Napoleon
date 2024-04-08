using System.Text.Json.Nodes;

namespace HugeCsv.Values;

public sealed class BoolValue : IKeyValue
{
    public BoolValue(bool value)
    {
        Value = value;
    }

    public KeyValueType Type => KeyValueType.Bool;
    public bool HasDefaultValue => !Value;
    public JsonValue? JsonValue => JsonValue.Create(Value);

    private bool Value { get; }
}