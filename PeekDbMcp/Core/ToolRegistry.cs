using System.Text.Json;

namespace PeekDbMcp.Core;

public static class ToolRegistry
{
    private static readonly List<McpToolDefinition> _tools = new();

    public static IReadOnlyList<McpToolDefinition> Tools => _tools.AsReadOnly();

    static ToolRegistry()
    {
        Register("list_tables",
            "Lists all user tables with schema, approximate row count, and space used.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("analyze_table_schema",
            "Returns the complete schema of a table: columns, data types, nullability, PKs, FKs, and indexes.",
            new
            {
                type = "object",
                properties = new
                {
                    table_name = new { type = "string", description = "Table name (optionally with schema, e.g. 'dbo.Users')" }
                },
                required = new[] { "table_name" }
            });

        Register("get_table_row_counts",
            "Returns approximate row counts, column counts, and index counts for each table, ordered by size.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_table_relationships",
            "Gets all foreign key relationships for a table or the entire database.",
            new
            {
                type = "object",
                properties = new
                {
                    table_name = new { type = "string", description = "Table name (optional; omit for all FKs)" }
                },
                required = Array.Empty<string>()
            });

        Register("list_stored_procedures",
            "Lists all stored procedures with schema and modification date. Not supported on PostgreSQL or SQLite.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_sp_definition",
            "Gets the source code (T-SQL) of a stored procedure. Not supported on PostgreSQL or SQLite.",
            new
            {
                type = "object",
                properties = new
                {
                    sp_name = new { type = "string", description = "Stored procedure name (optionally with schema)" }
                },
                required = new[] { "sp_name" }
            });

        Register("list_functions",
            "Lists all user-defined functions with schema, type (scalar/table), and dates.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_function_definition",
            "Gets the source code of a function (scalar, inline table, or table-valued).",
            new
            {
                type = "object",
                properties = new
                {
                    function_name = new { type = "string", description = "Function name (optionally with schema)" }
                },
                required = new[] { "function_name" }
            });

        Register("list_views",
            "Lists all user views with schema, creation date, and definition size.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_view_definition",
            "Gets the source code/definition of a view.",
            new
            {
                type = "object",
                properties = new
                {
                    view_name = new { type = "string", description = "View name (optionally with schema)" }
                },
                required = new[] { "view_name" }
            });

        Register("list_triggers",
            "Lists all triggers with schema, table, timing (before/after), and events. Not supported on SQLite.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_trigger_definition",
            "Gets the source code of a trigger. Not supported on SQLite.",
            new
            {
                type = "object",
                properties = new
                {
                    trigger_name = new { type = "string", description = "Trigger name (optionally with schema)" }
                },
                required = new[] { "trigger_name" }
            });

        Register("find_object_dependencies",
            "Finds the dependencies of an object: what objects it references and what references it.",
            new
            {
                type = "object",
                properties = new
                {
                    object_name = new { type = "string", description = "Object name (table, SP, function, view)" }
                },
                required = new[] { "object_name" }
            });

        Register("search_in_code",
            "Searches for text within the code of all programmatic objects (SPs, functions, triggers, views).",
            new
            {
                type = "object",
                properties = new
                {
                    keyword = new { type = "string", description = "Text to search for in object source code" }
                },
                required = new[] { "keyword" }
            });

        Register("get_missing_indexes",
            "Queries engine-suggested missing indexes (DMVs), ordered by impact. Not supported on SQLite.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("get_index_usage_stats",
            "Shows real usage statistics for existing indexes: seeks, scans, lookups, updates. Not supported on SQLite.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() });

        Register("analyze_query_plan",
            "Receives a SELECT query and returns the estimated execution plan (XML). Useful for performance analysis.",
            new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "SQL SELECT query to get the estimated execution plan for" }
                },
                required = new[] { "query" }
            });
    }

    private static void Register(string name, string description, object inputSchema)
    {
        var schemaJson = JsonSerializer.Serialize(inputSchema);
        _tools.Add(new McpToolDefinition
        {
            Name = name,
            Description = description,
            InputSchema = JsonDocument.Parse(schemaJson).RootElement.Clone()
        });
    }
}