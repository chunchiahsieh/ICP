namespace TEL.IntegrationHub.Models;

public class RoutingRule
{
    public long Id { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetSystem { get; set; } = string.Empty;

    public string TargetType { get; set; } = "Database";

    public string TargetName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
