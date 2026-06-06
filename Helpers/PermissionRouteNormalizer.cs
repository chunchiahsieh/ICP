namespace ICP.Helpers;

public static class PermissionRouteNormalizer
{
    public static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return string.Empty;
        }

        var normalized = route.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = $"/{normalized}";
        }

        if (normalized.EndsWith("/Index", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^6];
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    public static string ControllerToRoute(string? controller)
    {
        return string.IsNullOrWhiteSpace(controller)
            ? string.Empty
            : NormalizeRoute($"/{controller}");
    }
}
