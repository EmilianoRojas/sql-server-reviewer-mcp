using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;

namespace SqlServerMcp.Services;

public class DatabaseAnalyzer : IDisposable
{
    private readonly string _connectionString;

    public DatabaseAnalyzer(string connectionString)
    {
        _connectionString = connectionString;
    }

    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<IEnumerable<dynamic>> ListTablesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS [Schema],
                t.name AS [Table],
                p.rows AS ApproxRowCount,
                CAST(ROUND((SUM(a.total_pages) * 8.0) / 1024, 2) AS DECIMAL(18,2)) AS TotalSpaceMB,
                t.create_date AS Created,
                t.modify_date AS LastModified
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON t.object_id = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE t.is_ms_shipped = 0 AND i.index_id <= 1
            GROUP BY s.name, t.name, p.rows, t.create_date, t.modify_date
            ORDER BY s.name, t.name";

        return await conn.QueryAsync(sql);
    }

    public async Task<object> AnalyzeTableSchemaAsync(string tableName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, table) = ParseObjectName(tableName);

        const string columnsSql = @"
            SELECT 
                c.name AS ColumnName,
                tp.name AS DataType,
                c.max_length AS MaxLength,
                c.precision AS [Precision],
                c.scale AS Scale,
                c.is_nullable AS IsNullable,
                c.is_identity AS IsIdentity,
                dc.definition AS DefaultValue,
                CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
            FROM sys.columns c
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.index_columns ic
                INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.is_primary_key = 1
            ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
            WHERE t.name = @Table AND s.name = @Schema
            ORDER BY c.column_id";

        const string fksSql = @"
            SELECT 
                fk.name AS ForeignKeyName,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS [Column],
                OBJECT_SCHEMA_NAME(fkc.referenced_object_id) + '.' + OBJECT_NAME(fkc.referenced_object_id) AS ReferencedTable,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.name = @Table AND s.name = @Schema";

        const string indexesSql = @"
            SELECT 
                i.name AS IndexName,
                i.type_desc AS IndexType,
                i.is_unique AS IsUnique,
                i.is_primary_key AS IsPrimaryKey,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.name = @Table AND s.name = @Schema AND i.name IS NOT NULL
            GROUP BY i.name, i.type_desc, i.is_unique, i.is_primary_key";

        var columns = await conn.QueryAsync(columnsSql, new { Schema = schema, Table = table });
        var fks = await conn.QueryAsync(fksSql, new { Schema = schema, Table = table });
        var indexes = await conn.QueryAsync(indexesSql, new { Schema = schema, Table = table });

        return new { Table = $"{schema}.{table}", Columns = columns, ForeignKeys = fks, Indexes = indexes };
    }

    public async Task<string> GetSpDefinitionAsync(string spName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(spName);

        const string sql = @"
            SELECT m.definition
            FROM sys.sql_modules m
            INNER JOIN sys.procedures p ON m.object_id = p.object_id
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE p.name = @Name AND s.name = @Schema";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"Stored procedure '{schema}.{name}' not found.";
    }

    public async Task<IEnumerable<dynamic>> ListStoredProceduresAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS [Schema],
                p.name AS [Procedure],
                p.create_date AS Created,
                p.modify_date AS LastModified
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE p.is_ms_shipped = 0
            ORDER BY s.name, p.name";

        return await conn.QueryAsync(sql);
    }

    public async Task<string> GetFunctionDefinitionAsync(string functionName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(functionName);

        const string sql = @"
            SELECT m.definition
            FROM sys.sql_modules m
            INNER JOIN sys.objects o ON m.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.name = @Name AND s.name = @Schema
              AND o.type IN ('FN', 'IF', 'TF')";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"Function '{schema}.{name}' not found.";
    }

    public async Task<object> FindObjectDependenciesAsync(string objectName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(objectName);

        const string referencedBySql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(d.referencing_id) + '.' + OBJECT_NAME(d.referencing_id) AS ReferencingObject,
                o.type_desc AS ObjectType
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            WHERE d.referenced_entity_name = @Name
              AND (d.referenced_schema_name = @Schema OR d.referenced_schema_name IS NULL)";

        const string referencesSql = @"
            SELECT 
                ISNULL(d.referenced_schema_name, 'dbo') + '.' + d.referenced_entity_name AS ReferencedObject
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.name = @Name AND s.name = @Schema
            GROUP BY d.referenced_schema_name, d.referenced_entity_name";

        var referencedBy = await conn.QueryAsync(referencedBySql, new { Schema = schema, Name = name });
        var references = await conn.QueryAsync(referencesSql, new { Schema = schema, Name = name });

        return new { Object = $"{schema}.{name}", ReferencedBy = referencedBy, References = references };
    }

    public async Task<IEnumerable<dynamic>> SearchInCodeAsync(string keyword)
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(m.object_id) AS [Schema],
                OBJECT_NAME(m.object_id) AS ObjectName,
                o.type_desc AS ObjectType
            FROM sys.sql_modules m
            INNER JOIN sys.objects o ON m.object_id = o.object_id
            WHERE m.definition LIKE '%' + @Keyword + '%'
            ORDER BY o.type_desc, OBJECT_NAME(m.object_id)";

        return await conn.QueryAsync(sql, new { Keyword = keyword });
    }

    public async Task<IEnumerable<dynamic>> GetMissingIndexesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT TOP 20
                OBJECT_SCHEMA_NAME(mid.object_id) + '.' + OBJECT_NAME(mid.object_id) AS [Table],
                mid.equality_columns AS EqualityColumns,
                mid.inequality_columns AS InequalityColumns,
                mid.included_columns AS IncludedColumns,
                CAST(migs.avg_total_user_cost * migs.avg_user_impact * 
                     (migs.user_seeks + migs.user_scans) AS DECIMAL(18,2)) AS ImprovementMeasure,
                migs.user_seeks AS UserSeeks,
                migs.user_scans AS UserScans,
                migs.avg_total_user_cost AS AvgTotalUserCost,
                migs.avg_user_impact AS AvgUserImpact
            FROM sys.dm_db_missing_index_details mid
            INNER JOIN sys.dm_db_missing_index_groups mig ON mid.index_handle = mig.index_handle
            INNER JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
            WHERE mid.database_id = DB_ID()
            ORDER BY ImprovementMeasure DESC";

        return await conn.QueryAsync(sql);
    }

    public async Task<IEnumerable<dynamic>> GetTableRelationshipsAsync(string? tableName)
    {
        using var conn = await OpenConnectionAsync();

        var sql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS ParentTable,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ParentColumn,
                OBJECT_SCHEMA_NAME(fkc.referenced_object_id) + '.' + OBJECT_NAME(fkc.referenced_object_id) AS ReferencedTable,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn,
                fk.name AS ForeignKeyName
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id";

        if (!string.IsNullOrEmpty(tableName))
        {
            var (schema, table) = ParseObjectName(tableName);
            sql += @"
            INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE (t.name = @Table AND s.name = @Schema)
               OR (OBJECT_NAME(fkc.referenced_object_id) = @Table 
                   AND OBJECT_SCHEMA_NAME(fkc.referenced_object_id) = @Schema)";
            sql += "\n            ORDER BY ParentTable, fk.name";
            return await conn.QueryAsync(sql, new { Schema = schema, Table = table });
        }

        sql += "\n            ORDER BY ParentTable, fk.name";
        return await conn.QueryAsync(sql);
    }

    private static (string schema, string name) ParseObjectName(string fullName)
    {
        var parts = fullName.Split('.', 2);
        return parts.Length == 2
            ? (parts[0].Trim('[', ']', ' '), parts[1].Trim('[', ']', ' '))
            : ("dbo", parts[0].Trim('[', ']', ' '));
    }

    public void Dispose()
    {
        // Dapper manages connections per-call, nothing to dispose at service level
    }
}
