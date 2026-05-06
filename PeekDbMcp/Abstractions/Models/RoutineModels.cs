namespace PeekDbMcp.Abstractions.Models;

public class StoredProcedureInfo
{
    public string Schema { get; init; }
    public string Procedure { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
}

public class FunctionInfo
{
    public string Schema { get; init; }
    public string Function { get; init; }
    public string FunctionType { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
}

public class ViewInfo
{
    public string Schema { get; init; }
    public string View { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
    public int DefinitionLength { get; init; }
}

public class TriggerInfo
{
    public string Schema { get; init; }
    public string Trigger { get; init; }
    public string TableName { get; init; }
    public string TriggerTiming { get; init; }
    public string EventManipulation { get; init; }
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
}
