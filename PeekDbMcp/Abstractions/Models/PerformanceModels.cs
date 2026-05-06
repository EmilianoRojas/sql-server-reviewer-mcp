namespace PeekDbMcp.Abstractions.Models;

public class MissingIndex
{
    public string Table { get; init; }
    public string? EqualityColumns { get; init; }
    public string? InequalityColumns { get; init; }
    public string? IncludedColumns { get; init; }
    public decimal ImprovementMeasure { get; init; }
    public int UserSeeks { get; init; }
    public int UserScans { get; init; }
    public decimal AvgTotalUserCost { get; init; }
    public decimal AvgUserImpact { get; init; }
}

public class IndexUsageStats
{
    public string Table { get; init; }
    public string IndexName { get; init; }
    public string IndexType { get; init; }
    public int UserSeeks { get; init; }
    public int UserScans { get; init; }
    public int UserLookups { get; init; }
    public int UserUpdates { get; init; }
    public DateTime? LastSeek { get; init; }
    public DateTime? LastScan { get; init; }
    public string Assessment { get; init; }
}