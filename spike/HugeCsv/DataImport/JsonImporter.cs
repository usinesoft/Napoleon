using System.Globalization;
using System.Text.Json;
using HugeCsv.Values;

namespace HugeCsv.DataImport;

public class JsonImporter
{
    private readonly TableSchema _schema;

    public CompactTable Table { get; }


    public JsonImporter(TableSchema schema)
    {
        _schema = schema;
        Table = new(schema);
    }

    public void ImportJson(JsonDocument doc)
    {
        var row = new IKeyValue[_schema.Columns.Count];
        for (var i = 0; i < _schema.Columns.Count; i++)
        {
            var column = _schema.Columns[i];
            var jp = doc.RootElement.GetProperty(column.Name);
            if (jp.ValueKind == JsonValueKind.Undefined)
                throw new NotSupportedException($"Can not find property {column.Name}");

            IKeyValue? curValue;

            switch (jp.ValueKind)
            {
                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    throw new ArgumentException($"Property {column.Name} is not a scalar value");

                case JsonValueKind.String:
                    var str = jp.GetString();

                    if (DateTime.TryParse(str, CultureInfo.InvariantCulture, out var dt))
                        curValue = new DateValue(dt);
                    else
                        curValue = new StringValue(str!);
                    break;
                case JsonValueKind.Number:
                    double number = jp.GetDouble();
                    if (Math.Abs(number - (int)number) < double.Epsilon)
                    {
                        curValue = new IntValue((int)number);
                    }
                    else
                    {
                        curValue = new FloatValue(number);
                    }
                    break;
                case JsonValueKind.True:
                    curValue = new BoolValue(true);
                    break;
                case JsonValueKind.False:
                    curValue = new BoolValue(false);
                    break;
                case JsonValueKind.Null:
                    curValue = new NullValue();
                    break;
                default:
                    throw new NotSupportedException("Value kind not supported");
            }

            row[i] = curValue;

        }

        Table.AddRow(row);
    }
}