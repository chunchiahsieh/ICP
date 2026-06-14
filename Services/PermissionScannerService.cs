using System.Text.RegularExpressions;
using ICP.Helpers;

namespace ICP.Services;

public partial class PermissionScannerService
{
    private readonly IWebHostEnvironment _environment;

    public PermissionScannerService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public IReadOnlyList<ScannedPermission> Scan()
    {
        var viewsPath = Path.Combine(_environment.ContentRootPath, "Views");
        if (!Directory.Exists(viewsPath))
        {
            return [];
        }

        var results = new List<ScannedPermission>();

        foreach (var file in Directory.EnumerateFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(viewsPath, file).Replace('\\', '/');
            var content = File.ReadAllText(file);

            foreach (var match in PermissionTagRegex().Matches(content).Cast<Match>())
            {
                var tag = match.Groups["tag"].Value;
                var resourceCode = match.Groups["perm"].Value.Trim();
                if (string.IsNullOrWhiteSpace(resourceCode))
                {
                    continue;
                }

                var tagContent = match.Groups["tagContent"].Value;
                var permissionName = PermissionResourceNameResolver.Resolve(_environment, resourceCode);
                var route = DeriveRoute(relativePath) ?? ExtractRouteFromTag(tagContent);

                results.Add(new ScannedPermission
                {
                    ResourceCode = resourceCode,
                    ResourceName = permissionName,
                    ResourceType = PermissionResourceTypes.Resolve(tag, resourceCode),
                    Route = route,
                    Description = $"Auto-scanned from Views/{relativePath}",
                    SourceFile = relativePath
                });
            }
        }

        return results;
    }

    private static string? ExtractAttributeValue(string tagContent, string attributeName)
    {
        var doubleQuoted = new Regex(
            $@"\b{Regex.Escape(attributeName)}\s*=\s*""(?<value>(?:[^""\\]|\\.)*)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = doubleQuoted.Match(tagContent);
        if (match.Success)
        {
            return match.Groups["value"].Value;
        }

        var singleQuoted = new Regex(
            $@"\b{Regex.Escape(attributeName)}\s*=\s*'(?<value>(?:[^'\\]|\\.)*)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        match = singleQuoted.Match(tagContent);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ExtractRouteFromTag(string tagContent)
    {
        var controller = ExtractAttributeValue(tagContent, "asp-controller");
        if (string.IsNullOrWhiteSpace(controller))
        {
            return null;
        }

        var action = ExtractAttributeValue(tagContent, "asp-action");
        if (string.IsNullOrWhiteSpace(action) ||
            action.Equals("Index", StringComparison.OrdinalIgnoreCase))
        {
            return $"/{controller}";
        }

        return $"/{controller}/{action}";
    }

    private static string? DeriveRoute(string relativeViewPath)
    {
        if (relativeViewPath.StartsWith("Shared/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var withoutExt = relativeViewPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
            ? relativeViewPath[..^7]
            : relativeViewPath;

        var parts = withoutExt.Split('/');
        if (parts.Length >= 2 &&
            parts[0].Equals("Permission", StringComparison.OrdinalIgnoreCase))
        {
            parts = parts[1..];
        }

        if (parts.Length >= 2 &&
            parts[0].Equals("Setting", StringComparison.OrdinalIgnoreCase))
        {
            parts = parts[1..];
        }

        if (parts.Length >= 2 &&
            parts[0].Equals("FORWARDER", StringComparison.OrdinalIgnoreCase))
        {
            parts = parts[1..];
        }

        if (parts.Length > 0 &&
            parts[0].Equals("RoleResources", StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = "Resources";
        }

        if (parts.Length == 1)
        {
            return $"/{parts[0]}";
        }

        if (parts[^1].Equals("View", StringComparison.OrdinalIgnoreCase)
            && !parts[^1].Contains('.'))
        {
            return $"/{parts[0]}";
        }

        if (parts[^1].Equals("Index", StringComparison.OrdinalIgnoreCase))
        {
            return $"/{parts[0]}";
        }

        return $"/{string.Join('/', parts)}";
    }

    [GeneratedRegex(@"<(?<tag>[a-zA-Z][\w-]*)(?<tagContent>[^>]*)\bdata-permissions\s*=\s*[""'](?<perm>[^""']+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PermissionTagRegex();
}
