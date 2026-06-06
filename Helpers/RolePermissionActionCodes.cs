using ICP.Models.Icp;

namespace ICP.Helpers;

public static class RolePermissionActionCodes
{
    public const string Allow = "Allow";

    private static readonly HashSet<string> KnownActionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "View", "Create", "Delete", "Edit", "Scan", "Disable", "Export", "Approve"
    };

    public static string Resolve(Resource resource)
    {
        if (resource.ResourceType.Equals("Menu Category", StringComparison.OrdinalIgnoreCase)
            || resource.ResourceType.Equals("Menu", StringComparison.OrdinalIgnoreCase))
        {
            return Allow;
        }

        var segments = resource.ResourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length > 0 && KnownActionCodes.Contains(segments[^1]))
        {
            return segments[^1];
        }

        return Allow;
    }
}
