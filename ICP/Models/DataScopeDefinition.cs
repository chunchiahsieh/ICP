namespace ICP.Models;

public sealed class DataScopeDefinition
{
    public int Version { get; set; } = 1;

    public List<DataScopeCondition> Conditions { get; set; } = [];
}

public sealed class DataScopeCondition
{
    public string Table { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string Operator { get; set; } = "In";

    public List<string> Values { get; set; } = [];
}
