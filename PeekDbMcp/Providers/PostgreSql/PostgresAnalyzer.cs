using PeekDbMcp.Abstractions;
using PeekDbMcp.Abstractions.Models;
using Npgsql;
using Dapper;

namespace PeekDbMcp.Providers.PostgreSql;

public class PostgresAnalyzer : IDatabaseAnalyzer
{
    private readonly string _connectionString;

    public PostgresAnalyzer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ProviderCapabilities GetCapabilities() => new(
        SupportsStoredProcedures: false, // PostgreSQL uses functions, not SPs
        SupportsFunctions: true,
        SupportsTriggers: true,
        SupportsMissingIndexes: true,
        SupportsIndexStats: true,
        SupportsQueryPlan: true,
        ProviderName: "PostgreSql"
    );

    public async Task<IEnumerable<TableInfo>> ListTablesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                schemaname AS Schema,
                tablename AS Table,
                NULL::bigint AS ApproxRowCount,
                0::numeric AS TotalSpaceMB,
                NULL::timestamp AS Created,
                NULL::timestamp AS LastModified
            FROM pg_tables
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY schemaname, tablename";
        return await conn.QueryAsync<TableInfo>(sql);
    }

    public async Task<TableSchemaInfo> AnalyzeTableSchemaAsync(string tableName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, table) = ParseObjectName(tableName);

        const string columnsSql = @"
            SELECT 
                c.column_name AS ColumnName,
                c.data_type AS DataType,
                COALESCE(c.character_maximum_length, 0) AS MaxLength,
                COALESCE(c.numeric_precision, 0) AS Precision,
                COALESCE(c.numeric_scale, 0) AS Scale,
                c.is_nullable = 'YES' AS IsNullable,
                false AS IsIdentity,
                c.column_default AS DefaultValue,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS IsPrimaryKey
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_schema = @Schema AND c.table_name = @Table
            ORDER BY c.ordinal_position";

        const string fksSql = @"
            SELECT 
                tc.constraint_name AS ForeignKeyName,
                kcu.column_name AS Column,
                ccu.table_schema || '.' || ccu.table_name AS ReferencedTable,
                ccu.column_name AS ReferencedColumn
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @Schema AND tc.table_name = @Table";

        const string indexesSql = @"
            SELECT 
                i.relname AS IndexName,
                CASE WHEN ix.indisunique THEN 'UNIQUE' ELSE 'INDEX' END AS IndexType,
                ix.indisunique AS IsUnique,
                ix.indisprimary AS IsPrimaryKey,
                STRING_AGG(a.attname, ', ' ORDER BY x.n) AS Columns
            FROM pg_index ix
            JOIN pg_class i ON ix.indexrelid = i.oid
            JOIN pg_class t ON ix.indrelid = t.oid
            JOIN pg_namespace ns ON t.relnamespace = ns.oid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
            CROSS JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS x(val, n)
            WHERE ns.nspname = @Schema AND t.relname = @Table
            GROUP BY i.relname, ix.indisunique, ix.indisprimary";

        var columns = await conn.QueryAsync<ColumnInfo>(columnsSql, new { Schema = schema, Table = table });
        var fks = await conn.QueryAsync<ForeignKeyInfo>(fksSql, new { Schema = schema, Table = table });
        var indexes = await conn.QueryAsync<IndexInfo>(indexesSql, new { Schema = schema, Table = table });

        return new TableSchemaInfo($"{schema}.{table}", columns, fks, indexes);
    }

    public async Task<IEnumerable<TableRowCount>> GetTableRowCountsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                schemaname AS Schema,
                relname AS Table,
                NULL::bigint AS ApproxRowCount,
                0 AS ColumnCount,
                0 AS IndexCount
            FROM pg_stat_user_tables
            ORDER BY schemaname, relname";
        return await conn.QueryAsync<TableRowCount>(sql);
    }

    public async Task<IEnumerable<TableRelationship>> GetTableRelationshipsAsync(string? tableName)
    {
        using var conn = await OpenConnectionAsync();
        var sql = @"
            SELECT 
                tc.table_schema || '.' || tc.table_name AS ParentTable,
                kcu.column_name AS ParentColumn,
                ccu.table_schema || '.' || ccu.table_name AS ReferencedTable,
                ccu.column_name AS ReferencedColumn,
                tc.constraint_name AS ForeignKeyName
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'";

        if (!string.IsNullOrEmpty(tableName))
        {
            var (schema, table) = ParseObjectName(tableName);
            sql += " AND tc.table_schema = @Schema AND tc.table_name = @Table";
            return await conn.QueryAsync<TableRelationship>(sql, new { Schema = schema, Table = table });
        }

        return await conn.QueryAsync<TableRelationship>(sql);
    }

    public Task<IEnumerable<StoredProcedureInfo>> ListStoredProceduresAsync()
        => Task.FromResult(Enumerable.Empty<StoredProcedureInfo>());

    public Task<string> GetSpDefinitionAsync(string spName)
        => Task.FromResult("PostgreSQL does not have stored procedures. Use functions instead.");

    public async Task<IEnumerable<FunctionInfo>> ListFunctionsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                n.nspname AS Schema,
                p.proname AS Function,
                CASE 
                    WHEN p.prokind = 'w' THEN 'TF'
                    WHEN p.prorettype = 'pg_catalog.trigger'::pg_catalog.regtype THEN 'TF'
                    WHEN p.prorettype = 0 THEN 'IF'
                    ELSE 'FN'
                END AS FunctionType,
                CURRENT_TIMESTAMP AS Created,
                CURRENT_TIMESTAMP AS LastModified
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname = 'public'
              AND p.prokind = 'f'
            ORDER BY p.proname";
        return await conn.QueryAsync<FunctionInfo>(sql);
    }

    public async Task<string> GetFunctionDefinitionAsync(string functionName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(functionName);
        const string sql = @"
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname = @Schema AND p.proname = @Name";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"Function '{schema}.{name}' not found.";
    }

    public async Task<IEnumerable<ViewInfo>> ListViewsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                schemaname AS Schema,
                viewname AS View,
                NULL::timestamp AS Created,
                NULL::timestamp AS LastModified,
                0 AS DefinitionLength
            FROM pg_views
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY schemaname, viewname";
        return await conn.QueryAsync<ViewInfo>(sql);
    }

    public async Task<string> GetViewDefinitionAsync(string viewName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(viewName);
        const string sql = @"
            SELECT view_definition
            FROM information_schema.views
            WHERE table_schema = @Schema AND table_name = @Name";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"View '{schema}.{name}' not found.";
    }

    public async Task<IEnumerable<TriggerInfo>> ListTriggersAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                ns.nspname AS Schema,
                t.tgname AS Trigger,
                ns.nspname || '.' || c.relname AS TableName,
                CASE WHEN t.tgtype & 1 = 1 THEN 'BEFORE' ELSE 'AFTER' END AS TriggerTiming,
                CASE 
                    WHEN t.tgtype & 2 = 2 THEN 'INSERT'
                    WHEN t.tgtype & 4 = 4 THEN 'DELETE'
                    WHEN t.tgtype & 8 = 8 THEN 'UPDATE'
                    ELSE 'UNKNOWN'
                END AS EventManipulation,
                CURRENT_TIMESTAMP AS Created,
                CURRENT_TIMESTAMP AS LastModified
            FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            JOIN pg_namespace ns ON c.relnamespace = ns.oid
            WHERE ns.nspname = 'public' AND NOT t.tgisinternal
            ORDER BY t.tgname";
        return await conn.QueryAsync<TriggerInfo>(sql);
    }

    public async Task<string> GetTriggerDefinitionAsync(string triggerName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(triggerName);
        const string sql = @"
            SELECT pg_get_triggerdef(t.oid)
            FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            JOIN pg_namespace ns ON c.relnamespace = ns.oid
            WHERE t.tgname = @Name AND ns.nspname = @Schema";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Schema = schema, Name = name });
        return result ?? $"Trigger '{schema}.{name}' not found.";
    }

    public async Task<ObjectDependency> FindObjectDependenciesAsync(string objectName)
    {
        using var conn = await OpenConnectionAsync();
        var (schema, name) = ParseObjectName(objectName);

        const string referencedBySql = @"
            SELECT 
                d.refclassid::regclass::text AS ObjectName,
                'DEPENDENCY' AS ObjectType
            FROM pg_depend d
            WHERE d.classid = 'pg_class'::regclass AND d.objid = @Name::regclass";

        const string referencesSql = @"
            SELECT 
                d.classid::regclass::text AS ReferencedObject
            FROM pg_depend d
            WHERE d.refobjid = @Name::regclass";

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
                n.nspname AS Schema,
                p.proname AS ObjectName,
                'FUNCTION' AS ObjectType
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname = 'public'
              AND (p.proname LIKE '%' || @Keyword || '%' OR p.prosrc LIKE '%' || @Keyword || '%')
            ORDER BY p.proname";

        return await conn.QueryAsync<SearchResult>(sql, new { Keyword = keyword });
    }

    public async Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT TOP 20
                schemaname || '.' || relname AS Table,
                NULL AS EqualityColumns,
                NULL AS InequalityColumns,
                NULL AS IncludedColumns,
                0 AS ImprovementMeasure,
                0 AS UserSeeks,
                0 AS UserScans,
                0 AS AvgTotalUserCost,
                0 AS AvgUserImpact
            FROM pg_stat_user_indexes
            WHERE 1=0"; // Placeholder - real implementation needs pg_stat_user_indexes parsing
        return await conn.QueryAsync<MissingIndex>(sql);
    }

    public async Task<IEnumerable<IndexUsageStats>> GetIndexUsageStatsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                schemaname || '.' || relname AS Table,
                indexrelname AS IndexName,
                'INDEX' AS IndexType,
                idx_scan AS UserSeeks,
                idx_tup_read AS UserScans,
                0 AS UserLookups,
                idx_tup_write AS UserUpdates,
                NULL::timestamp AS LastSeek,
                NULL::timestamp AS LastScan,
                CASE 
                    WHEN idx_scan = 0 THEN 'No usage — consider removing'
                    ELSE 'In use'
                END AS Assessment
            FROM pg_stat_user_indexes
            ORDER BY idx_scan ASC";
        return await conn.QueryAsync<IndexUsageStats>(sql);
    }

    public async Task<string> AnalyzeQueryPlanAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Query cannot be empty.";
        if (!query.Trim().ToUpperInvariant().StartsWith("SELECT"))
            return "BLOCKED: Only SELECT queries are allowed.";

        using var conn = await OpenConnectionAsync();
        const string sql = "EXPLAIN (FORMAT JSON) @query";
        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Query = query });
        return result ?? "No plan returned.";
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static (string schema, string name) ParseObjectName(string fullName)
    {
        var parts = fullName.Split('.', 2);
        return parts.Length == 2
            ? (parts[0], parts[1])
            : ("public", parts[0]);
    }
}