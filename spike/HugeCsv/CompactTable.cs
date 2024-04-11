using System.Text;
using System.Text.Json.Nodes;
using HugeCsv.Values;

namespace HugeCsv
{
    public class CompactTable
    {
        /// <summary>
        /// If a primary key is present 
        /// </summary>
        private readonly Dictionary<IKeyValue, uint> _positionByPrimaryKey = new();

        private readonly List<IKeyValue?> _distinctValues = new(100_000);

        private readonly Dictionary<IKeyValue, uint> _positionForValue = [];

        private uint _lastPosition = 0;

        private readonly List<uint[]> _rows  = new(100_000);

        private uint ProcessKeyValue(IKeyValue kv)
        {
            if (_positionForValue.TryGetValue(kv, out var position))
            {
                return position;
            }

            _positionForValue[kv] = _lastPosition;

            _distinctValues.Add(kv);

            _lastPosition++;
            
            return _lastPosition-1;
        }

        private IKeyValue GetKeyValue(uint value)
        {
            return _distinctValues[(int)value] ?? throw new ArgumentException($"Invalid value {value}");
        }

        private readonly TableSchema _schema;

        private readonly int _indexOfPrimaryKey;

        public CompactTable(TableSchema schema)
        {
            _schema = schema;
            _indexOfPrimaryKey = schema.GetIndexOfPrimaryKey();
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

            if (_indexOfPrimaryKey != -1)
            {
                _positionByPrimaryKey[row[_indexOfPrimaryKey]] = (uint)(_rows.Count - 1);
            }
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

        public JsonObject GetByPrimaryKey(IKeyValue kv)
        {
            if (_indexOfPrimaryKey == -1) throw new NotSupportedException("No primary key defined");

            var position = _positionByPrimaryKey[kv];

            return GetItem((int)position);
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

    public record ColumnInfo(string Name, KeyValueType Type, bool IsPrimaryKey = false);


}
