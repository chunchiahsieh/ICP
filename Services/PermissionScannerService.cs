using System.Text.RegularExpressions;

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
            var route = DeriveRoute(relativePath);
            var content = File.ReadAllText(file);

            foreach (var match in PermissionTagRegex().Matches(content).Cast<Match>())
            {
                var tag = match.Groups["tag"].Value;
                var resourceCode = match.Groups["perm"].Value.Trim();
                if (string.IsNullOrWhiteSpace(resourceCode))
                {
                    continue;
                }

                // 從整個 opening tag 讀取屬性（data-permission-name 可能在 data-permissions 之後）
                var fullOpeningTag = match.Value;
                var permissionName = ExtractAttributeValue(fullOpeningTag, "data-permission-name");
                var innerText = ExtractInnerText(content, match.Index + match.Length, tag);

                results.Add(new ScannedPermission
                {
                    ResourceCode = resourceCode,
                    ResourceName = ResolveResourceName(permissionName, innerText, resourceCode),
                    ResourceType = MapResourceType(tag),
                    Route = route,
                    Description = $"Auto-scanned from Views/{relativePath}",
                    SourceFile = relativePath
                });
            }
        }

        return results;
    }

    private static string ResolveResourceName(string? permissionName, string? innerText, string resourceCode)
    {
        if (!string.IsNullOrWhiteSpace(permissionName))
        {
            return permissionName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(innerText))
        {
            return innerText.Trim();
        }

        return resourceCode;
    }

    private static string? ExtractAttributeValue(string tagContent, string attributeName)
    {
        var match = AttributeRegex(attributeName).Match(tagContent);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ExtractInnerText(string content, int startIndex, string tagName)
    {
        var closeTag = $"</{tagName}>";
        var closeIndex = content.IndexOf(closeTag, startIndex, StringComparison.OrdinalIgnoreCase);
        if (closeIndex < 0)
        {
            return null;
        }

        var inner = content[startIndex..closeIndex];
        inner = HtmlTagRegex().Replace(inner, " ");
        inner = RazorExpressionRegex().Replace(inner, " ");
        return string.IsNullOrWhiteSpace(inner) ? null : CollapseWhitespace(inner);
    }

    private static string CollapseWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value, " ").Trim();
    }

    private static string MapResourceType(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "button" => "Button",
            "a" => "Menu",
            "input" or "select" or "textarea" => "Field",
            "form" => "Page",
            _ => "Page"
        };
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
        if (parts.Length == 1)
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

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"@\{[\s\S]*?\}|@[\w.]+(?:\([^)]*\))?", RegexOptions.CultureInvariant)]
    private static partial Regex RazorExpressionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private static Regex AttributeRegex(string attributeName)
    {
        return new Regex(
            $@"\b{Regex.Escape(attributeName)}\s*=\s*[""'](?<value>[^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
