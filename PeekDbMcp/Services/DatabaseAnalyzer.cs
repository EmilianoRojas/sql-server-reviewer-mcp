using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;

namespace PeekDbMcp.Services;

public class DatabaseAnalyzer
{
    private readonly string _connectionString;
    private const int ConnectionTimeoutSeconds = 30;

    public DatabaseAnalyzer(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static string AppendConnectionTimeout(string connectionString)
    {
        // Only append if not already present
        if (connectionString.Contains("Connection Timeout=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Connect Timeout=", StringComparison.OrdinalIgnoreCase))
            return connectionString;
        return connectionString.TrimEnd(';') + $";Connection Timeout={ConnectionTimeoutSeconds}";
    }

    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var connString = AppendConnectionTimeout(_connectionString);
        var conn = new SqlConnection(connString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<IEnumerable<TableInfo>> ListTablesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS Schema,
                t.name AS Table,
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

        return await conn.QueryAsync<TableInfo>(sql);
    }

    public async Task<TableSchemaInfo> AnalyzeTableSchemaAsync(string tableName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, table) = ParseObjectName(tableName);

        const string columnsSql = @"
            SELECT 
                c.name AS ColumnName,
                tp.name AS DataType,
                c.max_length AS MaxLength,
                c.precision AS Precision,
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
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS Column,
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

        var columns = await conn.QueryAsync<ColumnInfo>(columnsSql, new { Schema = schema, Table = table });
        var fks = await conn.QueryAsync<ForeignKeyInfo>(fksSql, new { Schema = schema, Table = table });
        var indexes = await conn.QueryAsync<IndexInfo>(indexesSql, new { Schema = schema, Table = table });

        return new TableSchemaInfo($"{schema}.{table}", columns, fks, indexes);
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

    public async Task<IEnumerable<StoredProcedureInfo>> ListStoredProceduresAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS Schema,
                p.name AS Procedure,
                p.create_date AS Created,
                p.modify_date AS LastModified
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE p.is_ms_shipped = 0
            ORDER BY s.name, p.name";

        return await conn.QueryAsync<StoredProcedureInfo>(sql);
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

    public async Task<ObjectDependency> FindObjectDependenciesAsync(string objectName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(objectName);

        const string referencedBySql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(d.referencing_id) + '.' + OBJECT_NAME(d.referencing_id) AS ObjectName,
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

        var referencedBy = await conn.QueryAsync<ReferencingObject>(referencedBySql, new { Schema = schema, Name = name });
        var references = await conn.QueryAsync<string>(referencesSql, new { Schema = schema, Name = name });

        return new ObjectDependency($"{schema}.{name}", referencedBy, references);
    }

    public async Task<IEnumerable<SearchResult>> SearchInCodeAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length > 200)
            throw new ArgumentException("Keyword must be between 1 and 200 characters.");

        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(m.object_id) AS Schema,
                OBJECT_NAME(m.object_id) AS ObjectName,
                o.type_desc AS ObjectType
            FROM sys.sql_modules m
            INNER JOIN sys.objects o ON m.object_id = o.object_id
            WHERE m.definition LIKE '%' + @Keyword + '%'
            ORDER BY o.type_desc, OBJECT_NAME(m.object_id)";

        return await conn.QueryAsync<SearchResult>(sql, new { Keyword = keyword });
    }

    public async Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT TOP 20
                OBJECT_SCHEMA_NAME(mid.object_id) + '.' + OBJECT_NAME(mid.object_id) AS Table,
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

        return await conn.QueryAsync<MissingIndex>(sql);
    }

    public async Task<IEnumerable<TableRelationship>> GetTableRelationshipsAsync(string? tableName)
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
            return await conn.QueryAsync<TableRelationship>(sql, new { Schema = schema, Table = table });
        }

        sql += "\n            ORDER BY ParentTable, fk.name";
        return await conn.QueryAsync<TableRelationship>(sql);
    }

    public async Task<IEnumerable<IndexUsageStats>> GetIndexUsageStatsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT TOP 30
                OBJECT_SCHEMA_NAME(s.object_id) + '.' + OBJECT_NAME(s.object_id) AS Table,
                i.name AS IndexName,
                i.type_desc AS IndexType,
                s.user_seeks AS UserSeeks,
                s.user_scans AS UserScans,
                s.user_lookups AS UserLookups,
                s.user_updates AS UserUpdates,
                s.last_user_seek AS LastSeek,
                s.last_user_scan AS LastScan,
                CASE 
                    WHEN (s.user_seeks + s.user_scans + s.user_lookups) = 0 AND s.user_updates > 0 
                    THEN 'UNUSED - candidate for removal'
                    WHEN s.user_updates > (s.user_seeks + s.user_scans + s.user_lookups) * 10 
                    THEN 'LOW VALUE - updates >> reads'
                    ELSE 'OK'
                END AS Assessment
            FROM sys.dm_db_index_usage_stats s
            INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
            WHERE s.database_id = DB_ID()
              AND OBJECTPROPERTY(s.object_id, 'IsUserTable') = 1
              AND i.name IS NOT NULL
            ORDER BY (s.user_seeks + s.user_scans + s.user_lookups) ASC, s.user_updates DESC";

        return await conn.QueryAsync<IndexUsageStats>(sql);
    }

    public async Task<string> GetTriggerDefinitionAsync(string triggerName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(triggerName);

        const string sql = @"
            SELECT m.definition
            FROM sys.sql_modules m
            INNER JOIN sys.triggers t ON m.object_id = t.object_id
            LEFT JOIN sys.objects parent ON t.parent_id = parent.object_id
            LEFT JOIN sys.schemas s ON parent.schema_id = s.schema_id
            WHERE t.name = @Name
              AND (s.name = @Schema OR t.parent_id = 0)";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"Trigger '{name}' not found.";
    }

    public async Task<IEnumerable<ViewInfo>> ListViewsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS Schema,
                v.name AS View,
                v.create_date AS Created,
                v.modify_date AS LastModified,
                LEN(m.definition) AS DefinitionLength
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            INNER JOIN sys.sql_modules m ON v.object_id = m.object_id
            WHERE v.is_ms_shipped = 0
            ORDER BY s.name, v.name";

        return await conn.QueryAsync<ViewInfo>(sql);
    }

    public async Task<IEnumerable<TableRowCount>> GetTableRowCountsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                s.name AS Schema,
                t.name AS Table,
                p.rows AS ApproxRowCount,
                (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS ColumnCount,
                (SELECT COUNT(*) FROM sys.indexes i WHERE i.object_id = t.object_id AND i.index_id > 0) AS IndexCount
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
            WHERE t.is_ms_shipped = 0
            ORDER BY p.rows DESC";

        return await conn.QueryAsync<TableRowCount>(sql);
    }

    public async Task<string> AnalyzeQueryPlanAsync(string query)
    {
        // Security: validate the query before executing
        var validation = ValidateReadOnlyQuery(query);
        if (validation is not null)
            return $"BLOCKED: {validation}";

        using var conn = await OpenConnectionAsync();

        // SHOWPLAN_XML returns the estimated plan WITHOUT executing the query
        await conn.ExecuteAsync("SET SHOWPLAN_XML ON");

        try
        {
            var planXml = await conn.QueryFirstOrDefaultAsync<string>(query);
            return planXml ?? "No execution plan returned.";
        }
        finally
        {
            await conn.ExecuteAsync("SET SHOWPLAN_XML OFF");
        }
    }

    /// <summary>
    /// Validates that a query is read-only before allowing execution.
    /// Blocks DDL, DML, and dangerous statements.
    /// </summary>
    private static string? ValidateReadOnlyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Query cannot be empty.";

        var normalized = query.Trim().ToUpperInvariant();

        // Must start with SELECT, WITH, or be a simple query
        if (!normalized.StartsWith("SELECT") && !normalized.StartsWith("WITH") && !normalized.StartsWith("("))
            return "Only SELECT queries are allowed. Query must start with SELECT or WITH.";

        // Block dangerous keywords anywhere in the query
        string[] blockedKeywords = {
            "INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ", "CREATE ",
            "TRUNCATE ", "EXEC ", "EXECUTE ", "EXEC(", "EXECUTE(",
            "XP_", "SP_", "DBCC ", "GRANT ", "REVOKE ", "DENY ",
            "BACKUP ", "RESTORE ", "SHUTDOWN", "RECONFIGURE",
            "OPENROWSET", "OPENDATASOURCE", "OPENQUERY",
            "BULK ", "INTO ", " INTO ",
            "SET ", "WAITFOR ", "KILL ",
            "ENABLE ", "DISABLE ",
            "--", "/*", "*/",  // Block SQL comments (could hide malicious code)
            ";",               // Block statement separators (multi-statement injection)
        };

        foreach (var keyword in blockedKeywords)
        {
            if (normalized.Contains(keyword))
                return $"Blocked keyword detected: '{keyword.Trim()}'. Only read-only SELECT queries are allowed.";
        }

        return null; // Query is safe
    }

    private static (string schema, string name) ParseObjectName(string fullName)
    {
        var parts = fullName.Split('.', 2);
        return parts.Length == 2
            ? (parts[0].Trim('[', ']', ' '), parts[1].Trim('[', ']', ' '))
            : ("dbo", parts[0].Trim('[', ']', ' '));
    }
}
