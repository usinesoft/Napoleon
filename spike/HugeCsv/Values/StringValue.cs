using System.Text;
using System.Text.Json.Nodes;

namespace HugeCsv.Values;

public sealed class StringValue : IKeyValue
{
    public KeyValueType Type => KeyValueType.String;
    
    public bool HasDefaultValue => _utf8Bytes.Length == 0;
    
    public JsonValue JsonValue => JsonValue.Create(ToString());

    private readonly int _hash;

    private readonly byte[] _utf8Bytes;

    public StringValue(string value)
    {
        _hash = value.GetHashCode();

        _utf8Bytes = Encoding.UTF8.GetBytes(value);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not StringValue otherStringValue) return false;

        if (_hash != otherStringValue._hash)
        {
            return false;
        }

        if (_utf8Bytes.Length != otherStringValue._utf8Bytes.Length)
        {
            return false;
        }

        for (int i = 0; i < _utf8Bytes.Length; i++)
        {
            if (_utf8Bytes[i] != otherStringValue._utf8Bytes[i])
            {
                return false;
            }
        }

        return true;

    }

    public override int GetHashCode()
    {
        return _hash;
    }

    public override string ToString()
    {
        return Encoding.UTF8.GetString(_utf8Bytes);
    }
}