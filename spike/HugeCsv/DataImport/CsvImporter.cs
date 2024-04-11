using System.Text;
using HugeCsv.Values;

namespace HugeCsv.DataImport;

public class CsvImporter
{
    private TableSchema? _schema;

    private int _reportEvery = 10_000;

    private Action? _progressReporter;

    public void ReportProgressEvery(int lines, Action action)
    {
        _reportEvery = lines;

        _progressReporter = action;
    }

    public CompactTable ProcessFile(string fileName, string? primaryKeyColumn = null)
    {
        var separator = ',';

        CompactTable? table = null;

        int lineCount = 0;

        
        var first = true;
        foreach (var line in File.ReadLines(fileName))
        {
            // process the header
            if (first) 
            {
                separator = DetectSeparator(line);
                var columns = SplitCsvLine(line, separator);

                _schema = new();
                for (var index = 0; index < columns.Count; index++)
                {
                    var column = columns[index];
                    bool isPrimaryKey = primaryKeyColumn != null && column == primaryKeyColumn;
                    // in the final form null can not be the type of column
                    _schema.Columns.Add(new(column, KeyValueType.Null, isPrimaryKey));

                }


                first = false;

                table = new(_schema);

                continue;
            }


            // process the data rows
            var values = SplitCsvLine(line, separator);

            var typedValues = new List<IKeyValue>();

            
            for (var col = 0; col < _schema!.Columns.Count; col++)
            {
                var value = values[col];
                var kv = ValueFactory.Parse(value);

                typedValues.Add(kv);


                ////////////////////////////////////////////
                // adjust schema types 

                var columnInfo = _schema.Columns[col];

                // every real type overrides null
                if (columnInfo.Type == KeyValueType.Null && kv.Type != KeyValueType.Null)
                {
                    var info = columnInfo with { Type = kv.Type };
                    _schema.Columns[col] = info;
                }

                if (columnInfo.Type == KeyValueType.SomeInt && kv.Type == KeyValueType.SomeFloat)
                {
                    // every real type overrides null
                    var info = columnInfo with { Type = kv.Type };
                    _schema.Columns[col] = info;
                }
            }

            lineCount++;

            if (lineCount % _reportEvery == 0)
            {
                _progressReporter?.Invoke();
            }

            table!.AddRow(typedValues.ToArray());
            
        }

        return table!;
    }

    private static char DetectSeparator(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            throw new ArgumentException($"'{nameof(header)}' cannot be null or whitespace.", nameof(header));

        if (header.Contains(','))
            return ',';
        if (header.Contains(';'))
            return ';';
        if (header.Contains('\t')) return '\t';


        throw new FormatException($"Can not detect column separator from header {header}");
    }

    private static List<string> SplitCsvLine(string line, char separator)
    {
        var stringValues = new List<string>();

        var ignoreSeparator = false;

        var currentValue = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"') // ignore separator inside "" according to csv specification
            {
                ignoreSeparator = !ignoreSeparator;
            }
            else if (c == separator && !ignoreSeparator)
            {
                var stringValue = currentValue.ToString();
                stringValues.Add(stringValue);
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }
        
        stringValues.Add(currentValue.ToString());
        
        return stringValues;
    }
}