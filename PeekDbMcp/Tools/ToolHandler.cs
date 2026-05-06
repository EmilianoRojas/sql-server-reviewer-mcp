using System.Text.Json;
using Serilog;
using PeekDbMcp.Abstractions;
using PeekDbMcp.Core;

namespace PeekDbMcp.Tools;

public class ToolHandler : IToolHandler
{
    private readonly IDatabaseAnalyzer _db;

    public ToolHandler(IDatabaseAnalyzer db)
    {
        _db = db;
    }

    public async Task<McpToolCallResult> HandleAsync(string toolName, JsonElement arguments)
    {
        Log.Information("Executing tool: {Tool} with args: {Args}", toolName, arguments.ToString());

        try
        {
            // Check capabilities before executing
            var caps = _db.GetCapabilities();
            var (allowed, reason) = IsToolAllowed(toolName, caps);
            if (!allowed)
                return ErrorResult($"Tool '{toolName}' is not supported by {caps.ProviderName}. {reason}");

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
                "list_functions" => FormatJson(await _db.ListFunctionsAsync()),
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
                "list_triggers" => FormatJson(await _db.ListTriggersAsync()),
                "list_views" => FormatJson(await _db.ListViewsAsync()),
                "get_view_definition" => await _db.GetViewDefinitionAsync(
                    GetRequiredParam(arguments, "view_name")),
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
            return ErrorResult(ex.Message);
        }
    }

    private static (bool allowed, string? reason) IsToolAllowed(string toolName, ProviderCapabilities caps)
    {
        return toolName switch
        {
            "list_stored_procedures" or "get_sp_definition" =>
                caps.SupportsStoredProcedures
                    ? (true, null)
                    : (false, "PostgreSQL and SQLite do not have stored procedures. Use 'list_functions' instead."),

            "list_functions" or "get_function_definition" =>
                caps.SupportsFunctions
                    ? (true, null)
                    : (false, "This provider does not support user-defined functions."),

            "list_triggers" or "get_trigger_definition" =>
                caps.SupportsTriggers
                    ? (true, null)
                    : (false, "This provider does not support triggers."),

            "get_missing_indexes" =>
                caps.SupportsMissingIndexes
                    ? (true, null)
                    : (false, "This provider does not have missing index detection."),

            "get_index_usage_stats" =>
                caps.SupportsIndexStats
                    ? (true, null)
                    : (false, "This provider does not have index usage statistics."),

            "analyze_query_plan" =>
                caps.SupportsQueryPlan
                    ? (true, null)
                    : (false, "This provider does not support query plan analysis."),

            _ => (true, null)
        };
    }

    private static McpToolCallResult ErrorResult(string message) =>
        new()
        {
            IsError = true,
            Content = new List<McpContentBlock> { new() { Text = message } }
        };

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