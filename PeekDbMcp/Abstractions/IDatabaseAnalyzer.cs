using System.Threading.Tasks;
using PeekDbMcp.Abstractions.Models;

namespace PeekDbMcp.Abstractions;

public interface IDatabaseAnalyzer
{
    // Table & Schema
    Task<IEnumerable<TableInfo>> ListTablesAsync();
    Task<TableSchemaInfo> AnalyzeTableSchemaAsync(string tableName);
    Task<IEnumerable<TableRowCount>> GetTableRowCountsAsync();

    // Relationships
    Task<IEnumerable<TableRelationship>> GetTableRelationshipsAsync(string? tableName);

    // Routines (SPs, Functions, Views, Triggers)
    Task<IEnumerable<StoredProcedureInfo>> ListStoredProceduresAsync();
    Task<string> GetSpDefinitionAsync(string spName);
    Task<IEnumerable<FunctionInfo>> ListFunctionsAsync();
    Task<string> GetFunctionDefinitionAsync(string functionName);
    Task<IEnumerable<ViewInfo>> ListViewsAsync();
    Task<string> GetViewDefinitionAsync(string viewName);
    Task<IEnumerable<TriggerInfo>> ListTriggersAsync();
    Task<string> GetTriggerDefinitionAsync(string triggerName);

    // Dependencies & Search
    Task<ObjectDependency> FindObjectDependenciesAsync(string objectName);
    Task<IEnumerable<SearchResult>> SearchInCodeAsync(string keyword);

    // Performance
    Task<IEnumerable<MissingIndex>> GetMissingIndexesAsync();
    Task<IEnumerable<IndexUsageStats>> GetIndexUsageStatsAsync();
    Task<string> AnalyzeQueryPlanAsync(string query);

    // Capability query
    ProviderCapabilities GetCapabilities();
}

public record ProviderCapabilities(
    bool SupportsStoredProcedures,
    bool SupportsFunctions,
    bool SupportsTriggers,
    bool SupportsMissingIndexes,
    bool SupportsIndexStats,
    bool SupportsQueryPlan,
    string ProviderName
);