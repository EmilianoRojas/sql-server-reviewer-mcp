using System.Text.Json;
using Serilog;
using SqlServerMcp.Core;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

public class ToolHandler : IToolHandler
{
    private readonly DatabaseAnalyzer _db;

    public ToolHandler(DatabaseAnalyzer db)
    {
        _db = db;
    }

    public async Task<McpToolCallResult> HandleAsync(string toolName, JsonElement arguments)
    {
        Log.Information("Executing tool: {Tool} with args: {Args}", toolName, arguments.ToString());

        try
        {
            var resultText = toolName switch
            {
                "list_tables" => FormatJson(await _db.ListTablesAsync()),
                "analyze_table_schema" => FormatJson(await _db.AnalyzeTableSchemaAsync(
                    GetRequiredParam(arguments, "table_name"))),
                "get_sp_definition" => await _db.GetSpDefinitionAsync(
                    GetRequiredParam(arguments, "sp_name")),
                "list_stored_procedures" => FormatJson(await _db.ListStoredProceduresAsync()),
                "get_function_definition" => await _db.GetFunctionDefinitionAsync(
                    GetRequiredParam(arguments, "function_name")),
                "find_object_dependencies" => FormatJson(await _db.FindObjectDependenciesAsync(
                    GetRequiredParam(arguments, "object_name"))),
                "search_in_code" => FormatJson(await _db.SearchInCodeAsync(
                    GetRequiredParam(arguments, "keyword"))),
                "get_missing_indexes" => FormatJson(await _db.GetMissingIndexesAsync()),
                "get_table_relationships" => FormatJson(await _db.GetTableRelationshipsAsync(
                    GetOptionalParam(arguments, "table_name"))),
                "get_index_usage_stats" => FormatJson(await _db.GetIndexUsageStatsAsync()),
                "get_trigger_definition" => await _db.GetTriggerDefinitionAsync(
                    GetRequiredParam(arguments, "trigger_name")),
                "list_views" => FormatJson(await _db.ListViewsAsync()),
                "get_table_row_counts" => FormatJson(await _db.GetTableRowCountsAsync()),
                "analyze_query_plan" => await _db.AnalyzeQueryPlanAsync(
                    GetRequiredParam(arguments, "query")),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };

            return new McpToolCallResult
            {
                Content = new List<McpContentBlock> { new() { Text = resultText } }
            };
        }
        catch (ArgumentException ex)
        {
            return new McpToolCallResult
            {
                IsError = true,
                Content = new List<McpContentBlock> { new() { Text = ex.Message } }
            };
        }
    }

    private static string GetRequiredParam(JsonElement args, string name)
    {
        if (args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString()!;

        throw new ArgumentException($"Missing required parameter: '{name}'");
    }

    private static string? GetOptionalParam(JsonElement args, string name)
    {
        if (args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static string FormatJson(object data)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
