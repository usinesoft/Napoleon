using System.Text.Json.Nodes;

namespace HugeCsv.Values;

public sealed class IntValue : IKeyValue
{
    public KeyValueType Type => KeyValueType.SomeInt;
    
    public bool HasDefaultValue => Value == 0;
    public JsonValue JsonValue => JsonValue.Create(Value);

    public long Value { get; }

    public IntValue(int value)
    {
        Value = value;
    }

    public IntValue(long value)
    {
        Value = value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is IntValue otherIntValue)
        {
            return Value == otherIntValue.Value;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}