namespace PeekDbMcp.Abstractions.Models;

public class TableInfo
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public int? ApproxRowCount { get; init; }
    public decimal TotalSpaceMB { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
}