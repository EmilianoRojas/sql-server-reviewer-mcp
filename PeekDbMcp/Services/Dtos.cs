namespace PeekDbMcp.Services;

// ============================================
// TABLE & SCHEMA DTOs
// ============================================

public record TableInfo(
    string Schema,
    string Table,
    int? ApproxRowCount,
    decimal TotalSpaceMB,
    DateTime Created,
    DateTime LastModified
);

public record TableSchemaInfo(
    string Table,
    IEnumerable<ColumnInfo> Columns,
    IEnumerable<ForeignKeyInfo> ForeignKeys,
    IEnumerable<IndexInfo> Indexes
);

public record ColumnInfo(
    string ColumnName,
    string DataType,
    int MaxLength,
    int Precision,
    int Scale,
    bool IsNullable,
    bool IsIdentity,
    string? DefaultValue,
    bool IsPrimaryKey
);

public record ForeignKeyInfo(
    string ForeignKeyName,
    string Column,
    string ReferencedTable,
    string ReferencedColumn
);

public record IndexInfo(
    string IndexName,
    string IndexType,
    bool IsUnique,
    bool IsPrimaryKey,
    string Columns
);

// ============================================
// ROUTINE DTOs
// ============================================

public record StoredProcedureInfo(
    string Schema,
    string Procedure,
    DateTime Created,
    DateTime LastModified
);

public record ViewInfo(
    string Schema,
    string View,
    DateTime Created,
    DateTime LastModified,
    int DefinitionLength
);

public record TriggerInfo(
    string Schema,
    string Trigger,
    string Definition
);

// ============================================
// RELATIONSHIP & DEPENDENCY DTOs
// ============================================

public record TableRelationship(
    string ParentTable,
    string ParentColumn,
    string ReferencedTable,
    string ReferencedColumn,
    string ForeignKeyName
);

public record ObjectDependency(
    string Object,
    IEnumerable<ReferencingObject> ReferencedBy,
    IEnumerable<string> References
);

public record ReferencingObject(
    string ObjectName,
    string ObjectType
);

// ============================================
// PERFORMANCE DTOs
// ============================================

public record MissingIndex(
    string Table,
    string? EqualityColumns,
    string? InequalityColumns,
    string? IncludedColumns,
    decimal ImprovementMeasure,
    int UserSeeks,
    int UserScans,
    decimal AvgTotalUserCost,
    decimal AvgUserImpact
);

public record IndexUsageStats(
    string Table,
    string IndexName,
    string IndexType,
    int UserSeeks,
    int UserScans,
    int UserLookups,
    int UserUpdates,
    DateTime? LastSeek,
    DateTime? LastScan,
    string Assessment
);

public record TableRowCount(
    string Schema,
    string Table,
    int ApproxRowCount,
    int ColumnCount,
    int IndexCount
);

public record SearchResult(
    string Schema,
    string ObjectName,
    string ObjectType
);
