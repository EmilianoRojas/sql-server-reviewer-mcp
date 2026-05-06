using PeekDbMcp.Abstractions;
using PeekDbMcp.Abstractions.Models;
using Microsoft.Data.Sqlite;
using Dapper;

namespace PeekDbMcp.Providers.Sqlite;

public class SqliteAnalyzer : IDatabaseAnalyzer
{
    private readonly string _connectionString;

    public SqliteAnalyzer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ProviderCapabilities GetCapabilities() => new(
        SupportsStoredProcedures: false,
        SupportsFunctions: false,
        SupportsTriggers: false,
        SupportsMissingIndexes: false,
        SupportsIndexStats: false,
        SupportsQueryPlan: true,
        ProviderName: "Sqlite"
    );

    public async Task<IEnumerable<TableInfo>> ListTablesAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                'main' AS Schema,
                name AS 'Table',
                0 AS ApproxRowCount,
                0 AS TotalSpaceMB,
                CURRENT_TIMESTAMP AS Created,
                CURRENT_TIMESTAMP AS LastModified
            FROM sqlite_master
            WHERE type = 'table' AND tbl_name NOT LIKE 'sqlite_%'
            ORDER BY tbl_name";
        return await conn.QueryAsync<TableInfo>(sql);
    }

    public async Task<TableSchemaInfo> AnalyzeTableSchemaAsync(string tableName)
    {
        using var conn = await OpenConnectionAsync();
        var (_, table) = ParseObjectName(tableName);

        var columnsSql = $"SELECT name AS ColumnName, type AS DataType, 0 AS MaxLength, 0 AS Precision, 0 AS Scale, (CASE WHEN [notnull] = 0 THEN 1 ELSE 0 END) AS IsNullable, 0 AS IsIdentity, dflt_value AS DefaultValue, pk AS IsPrimaryKey FROM pragma_table_info('{table}')";
        var fksSql = $"SELECT '' AS ForeignKeyName, 'from' AS Column, 'to' AS ReferencedTable, 'to' AS ReferencedColumn FROM pragma_foreign_key_list('{table}')";
        var indexesSql = $"SELECT name AS IndexName, '' AS IndexType, 0 AS IsUnique, 0 AS IsPrimaryKey, '' AS Columns FROM pragma_index_list('{table}')";

        var columns = await conn.QueryAsync<ColumnInfo>(columnsSql, new { Table = table });
        var fks = await conn.QueryAsync<ForeignKeyInfo>(fksSql, new { Table = table });
        var indexes = await conn.QueryAsync<IndexInfo>(indexesSql, new { Table = table });

        return new TableSchemaInfo(table, columns, fks, indexes);
    }

    public async Task<IEnumerable<TableRowCount>> GetTableRowCountsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                'main' AS Schema,
                name AS 'Table',
                0 AS ApproxRowCount,
                (SELECT COUNT(*) FROM pragma_table_info(name)) AS ColumnCount,
                (SELECT COUNT(*) FROM pragma_index_list(name)) AS IndexCount
            FROM sqlite_master
            WHERE type = 'table' AND tbl_name NOT LIKE 'sqlite_%'
            ORDER BY tbl_name";
        return await conn.QueryAsync<TableRowCount>(sql);
    }

    public async Task<IEnumerable<TableRelationship>> GetTableRelationshipsAsync(string? tableName)
    {
        using var conn = await OpenConnectionAsync();
        var sql = "PRAGMA foreign_key_list";
        
        if (!string.IsNullOrEmpty(tableName))
        {
            var (_, table) = ParseObjectName(tableName);
            return await conn.QueryAsync<TableRelationship>(sql);
        }

        return await conn.QueryAsync<TableRelationship>(sql);
    }

    public Task<IEnumerable<StoredProcedureInfo>> ListStoredProceduresAsync()
        => Task.FromResult(Enumerable.Empty<StoredProcedureInfo>());

    public Task<string> GetSpDefinitionAsync(string spName)
        => Task.FromResult("SQLite does not support stored procedures.");

    public Task<IEnumerable<FunctionInfo>> ListFunctionsAsync()
        => Task.FromResult(Enumerable.Empty<FunctionInfo>());

    public Task<string> GetFunctionDefinitionAsync(string functionName)
        => Task.FromResult("SQLite does not support user-defined functions in the same way.");

    public async Task<IEnumerable<ViewInfo>> ListViewsAsync()
    {
        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                'main' AS Schema,
                name AS View,
                CURRENT_TIMESTAMP AS Created,
                CURRENT_TIMESTAMP AS LastModified,
                0 AS DefinitionLength
            FROM sqlite_master
            WHERE type = 'view'
            ORDER BY tbl_name";
        return await conn.QueryAsync<ViewInfo>(sql);
    }

    public async Task<string> GetViewDefinitionAsync(string viewName)
    {
        using var conn = await OpenConnectionAsync();
        var (_, name) = ParseObjectName(viewName);
        const string sql = @"
            SELECT sql FROM sqlite_master WHERE type = 'view' AND name = @Name";

        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Name = name });
        return result ?? $"View '{name}' not found.";
    }

    public Task<IEnumerable<TriggerInfo>> ListTriggersAsync()
        => Task.FromResult(Enumerable.Empty<TriggerInfo>());

    public Task<string> GetTriggerDefinitionAsync(string triggerName)
        => Task.FromResult("SQLite does not support triggers in the same way.");

    public async Task<ObjectDependency> FindObjectDependenciesAsync(string objectName)
    {
        using var conn = await OpenConnectionAsync();
        var (_, name) = ParseObjectName(objectName);

        const string sql = @"
            SELECT 
                name AS ObjectName,
                type AS ObjectType
            FROM sqlite_master
            WHERE sql LIKE '%' || @Name || '%'";

        var referencedBy = await conn.QueryAsync<ReferencingObject>(sql, new { Name = name });
        return new ObjectDependency(name, referencedBy, Enumerable.Empty<string>());
    }

    public async Task<IEnumerable<SearchResult>> SearchInCodeAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length > 200)
            throw new ArgumentException("Keyword must be between 1 and 200 characters.");

        using var conn = await OpenConnectionAsync();
        const string sql = @"
            SELECT 
                'main' AS Schema,
                name AS ObjectName,
                type AS ObjectType
            FROM sqlite_master
            WHERE sql LIKE '%' || @Keyword || '%'
            ORDER BY type, name";

        return await conn.QueryAsync<SearchResult>(sql, new { Keyword = keyword });
    }

    public Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync()
        => Task.FromResult(Enumerable.Empty<MissingIndex>());

    public Task<IEnumerable<IndexUsageStats>> GetIndexUsageStatsAsync()
        => Task.FromResult(Enumerable.Empty<IndexUsageStats>());

    public async Task<string> AnalyzeQueryPlanAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Query cannot be empty.";
        if (!query.Trim().ToUpperInvariant().StartsWith("SELECT"))
            return "BLOCKED: Only SELECT queries are allowed.";

        using var conn = await OpenConnectionAsync();
        const string sql = "EXPLAIN QUERY PLAN @query";
        var result = await conn.QueryFirstOrDefaultAsync<string>(sql, new { Query = query });
        return result ?? "No plan returned.";
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static (string schema, string name) ParseObjectName(string fullName)
    {
        var parts = fullName.Split('.', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("main", parts[0]);
    }
}