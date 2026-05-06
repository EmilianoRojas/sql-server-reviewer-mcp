namespace PeekDbMcp.Abstractions.Models;

public class TableRelationship
{
    public string ParentTable { get; init; }
    public string ParentColumn { get; init; }
    public string ReferencedTable { get; init; }
    public string ReferencedColumn { get; init; }
    public string ForeignKeyName { get; init; }
}

public class ObjectDependency
{
    public string Object { get; init; }
    public IEnumerable<ReferencingObject> ReferencedBy { get; init; }
    public IEnumerable<string> References { get; init; }

    public ObjectDependency() { }
    public ObjectDependency(string obj, IEnumerable<ReferencingObject> referencedBy,
        IEnumerable<string> references)
    {
        Object = obj;
        ReferencedBy = referencedBy;
        References = references;
    }
}

public class ReferencingObject
{
    public string ObjectName { get; init; }
    public string ObjectType { get; init; }
}

public class SearchResult
{
    public string Schema { get; init; }
    public string ObjectName { get; init; }
    public string ObjectType { get; init; }
}