using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HugeCsv.Values;

namespace HugeCsv
{
    public class CompactTable
    {
        private readonly List<IKeyValue?> _distinctValues = new(100_000);

        private readonly Dictionary<IKeyValue, uint> _positionForValue = [];

        private uint _lastPosition = 256;

        private readonly List<uint[]> _rows  = new(100_000);

        private uint ProcessKeyValue(IKeyValue kv)
        {
            if (_positionForValue.TryGetValue(kv, out var position))
            {
                return position;
            }

            _lastPosition++;

            _positionForValue[kv] = _lastPosition;

            _distinctValues.Add(kv);

            return _lastPosition;
        }

        private IKeyValue GetKeyValue(uint value)
        {
            return _distinctValues[(int)value] ?? throw new ArgumentException($"Invalid value {value}");
        }

        private readonly TableSchema _schema;

        public CompactTable(TableSchema schema)
        {
            _schema = schema;
        }

        public void AddRow(IKeyValue[] row)
        {
            var columns = row.Length;
            if (columns != _schema.Columns.Count)
            {
                throw new ArgumentException("Invalid row length");
            }

            var compactRow = new uint[columns];
            for (int i = 0; i < columns ; i++)
            {
                compactRow[i] = ProcessKeyValue(row[i]);
            }

            _rows.Add(compactRow);
        }

        /// <summary>
        /// Return an item by index as a json object
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public JsonObject GetItem(int index)
        {
            var row = _rows[index];

            var result = new JsonObject();

            var columns = row.Length;
            for (int i = 0; i < columns; i++)
            {
                var name = _schema.Columns[i].Name;
                var value = GetKeyValue(row[i]).JsonValue;

                result.Add(name, value);
            }

            return result;
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendLine($"Lines            ={_rows.Count:N0}");
            result.AppendLine($"Columns          ={_schema.Columns.Count}");
            result.AppendLine($"Distinct values  ={_distinctValues.Count:N0}");
            result.AppendLine();
            result.AppendLine("schema:");
            foreach (var columnInfo in _schema.Columns)
            {
                result.AppendLine($"{columnInfo.Name.PadRight(40)}:{columnInfo.Type}");
            }

            return result.ToString();
        }

        public int LineCount => _rows.Count;

    }

    /// <summary>
    /// Ordered description of the columns in a table
    /// </summary>
    public class TableSchema
    {
        public IList<ColumnInfo> Columns { get; } = new List<ColumnInfo>();
    }

    public record ColumnInfo(string Name, KeyValueType Type);


}
