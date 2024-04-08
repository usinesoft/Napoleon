using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HugeCsv.Values;

public sealed class DateValue:IKeyValue
{
    public KeyValueType Type => KeyValueType.Date;
    public bool HasDefaultValue => false;

    public JsonValue JsonValue
    {
        get
        {
            var jsonString = $""" "{ToString()}" """;
            return JsonValue.Create(jsonString);
        }
    }

    private readonly long _utcTicks;

    /// <summary>
    /// If null, unspecified time zone, display as is
    /// If zero then it is a UTC time
    /// Otherwise represents the offset of the local time zone
    /// </summary>
    private readonly long? _offset = null;

    public DateValue(DateTime value)
    {
        // if no time information assume only the date is important so ignore the timezone
        if (value == value.Date)
        {
            _utcTicks = value.Ticks;
            return;
        }

        switch (value.Kind)
        {
            case DateTimeKind.Utc:
                _utcTicks = value.Ticks;
                _offset = 0;
                return;
            case DateTimeKind.Unspecified:
                _utcTicks = value.Ticks;
                return;
            case DateTimeKind.Local:
                _utcTicks = value.ToUniversalTime().Ticks;
                _offset = TimeZoneInfo.Local.BaseUtcOffset.Ticks;
                break;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is DateValue otherDate)
        {
            return _utcTicks == otherDate._utcTicks;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _utcTicks.GetHashCode();
    }

    public override string ToString()
    {
        var date = new DateTimeOffset(_utcTicks, new TimeSpan(_offset??0));

        return date.ToString(date == date.Date ? // date only
            "yyyy-MM-dd" : "o");
    }
}