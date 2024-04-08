using System.Text.Json.Nodes;

namespace HugeCsv.Values;

public sealed class FloatValue : IKeyValue
{
    const int Precision = 5;

    public KeyValueType Type => KeyValueType.SomeFloat;
    
    public bool HasDefaultValue => (int)( Value * (10 ^ Precision)) == 0;

    public JsonValue JsonValue => JsonValue.Create(Value);

    private double Value { get; }

    public FloatValue(double value)
    {
        Value = value;
    }


    public override bool Equals(object? obj)
    {
        const long multiplier = 10 ^ Precision;

        switch (obj)
        {
            case IntValue otherIntValue:
                unchecked
                {
                    return (long)Value * multiplier == otherIntValue.Value * multiplier;
                }
            case FloatValue otherFloatValue:
                unchecked
                {
                    return (long)(Value * multiplier) == (long)(otherFloatValue.Value * multiplier);
                }
            default:
                return false;
        }
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString($"D:{Precision}");
    }
}