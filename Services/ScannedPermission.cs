namespace ICP.Services;

public class ScannedPermission
{
    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? Route { get; set; }

    public string? Description { get; set; }

    public string SourceFile { get; set; } = string.Empty;
}
