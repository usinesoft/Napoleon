using HugeCsv.Values;

namespace HugeCsv;

/// <summary>
/// Ordered description of the columns in a table
/// </summary>
public class TableSchema
{
        
    public IList<ColumnInfo> Columns { get; } = new List<ColumnInfo>();

    public int GetIndexOfPrimaryKey()
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].IsPrimaryKey)
                return i;
        }

        return -1;
    }
}

public static class FluentExtensions
{
    public static TableSchema AddColumn(this TableSchema @this,  string name, KeyValueType columnType)
    {
        @this.Columns.Add(new ColumnInfo(name, columnType));
        return @this;
    }

    public static TableSchema AddPrimaryKey(this TableSchema @this,  string name, KeyValueType columnType)
    {
        @this.Columns.Add(new ColumnInfo(name, columnType, true));
        return @this;
    }
}