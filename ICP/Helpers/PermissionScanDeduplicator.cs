using ICP.Services;

namespace ICP.Helpers;

public static class PermissionScanDeduplicator
{
    public static IReadOnlyList<ScannedPermission> DeduplicateByResourceCode(
        IEnumerable<ScannedPermission> items)
    {
        return items
            .GroupBy(x => x.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => ResourceTypePriority(x.ResourceType)).First())
            .ToList();
    }

    private static int ResourceTypePriority(string resourceType) => resourceType switch
    {
        "Button" => 0,
        "Menu" => 1,
        "Menu Category" => 2,
        "Field" => 3,
        _ => 4
    };
}
