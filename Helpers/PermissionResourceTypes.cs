namespace ICP.Helpers;

public static class PermissionResourceTypes
{
    public static string Resolve(string tag, string resourceCode)
    {
        if (IsMenuCategory(tag, resourceCode))
        {
            return "Menu Category";
        }

        return tag.ToLowerInvariant() switch
        {
            "button" => "Button",
            "a" => "Menu",
            "input" or "select" or "textarea" => "Field",
            "form" => "Page",
            _ => "Page"
        };
    }

    private static bool IsMenuCategory(string tag, string resourceCode)
    {
        if (!tag.Equals("div", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = resourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 4
            && segments[0].Equals("Views", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("Shared", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("_SidebarNav", StringComparison.OrdinalIgnoreCase);
    }
}
