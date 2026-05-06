namespace PeekDbMcp.Abstractions.Models;

public class TableSchemaInfo
{
    public string Table { get; init; }
    public IEnumerable<ColumnInfo> Columns { get; init; }
    public IEnumerable<ForeignKeyInfo> ForeignKeys { get; init; }
    public IEnumerable<IndexInfo> Indexes { get; init; }

    public TableSchemaInfo() { }
    public TableSchemaInfo(string table, IEnumerable<ColumnInfo> columns,
        IEnumerable<ForeignKeyInfo> foreignKeys, IEnumerable<IndexInfo> indexes)
    {
        Table = table;
        Columns = columns;
        ForeignKeys = foreignKeys;
        Indexes = indexes;
    }
}

public class ColumnInfo
{
    public string ColumnName { get; init; }
    public string DataType { get; init; }
    public int MaxLength { get; init; }
    public int Precision { get; init; }
    public int Scale { get; init; }
    public bool IsNullable { get; init; }
    public bool IsIdentity { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsPrimaryKey { get; init; }
}

public class ForeignKeyInfo
{
    public string ForeignKeyName { get; init; }
    public string Column { get; init; }
    public string ReferencedTable { get; init; }
    public string ReferencedColumn { get; init; }
}

public class IndexInfo
{
    public string IndexName { get; init; }
    public string IndexType { get; init; }
    public bool IsUnique { get; init; }
    public bool IsPrimaryKey { get; init; }
    public string Columns { get; init; }
}

public class TableRowCount
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public int ApproxRowCount { get; init; }
    public int ColumnCount { get; init; }
    public int IndexCount { get; init; }
}