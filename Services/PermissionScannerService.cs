using System.Text.RegularExpressions;
using ICP;
using Microsoft.Extensions.Localization;

namespace ICP.Services;

public partial class PermissionScannerService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PermissionScannerService(
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer)
    {
        _environment = environment;
        _localizer = localizer;
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

                var fullOpeningTag = match.Value;
                var permissionKey = ExtractAttributeValue(fullOpeningTag, "data-permission-key");
                var permissionName = ResolveResourceName(permissionKey, fullOpeningTag, content, match, tag, resourceCode);
                var innerText = ExtractInnerText(content, match.Index + match.Length, tag);

                if (string.IsNullOrWhiteSpace(permissionName) && !string.IsNullOrWhiteSpace(innerText))
                {
                    permissionName = innerText.Trim();
                }

                if (string.IsNullOrWhiteSpace(permissionName) || IsBrokenLocalizerName(permissionName))
                {
                    permissionName = resourceCode;
                }

                results.Add(new ScannedPermission
                {
                    ResourceCode = resourceCode,
                    ResourceName = permissionName,
                    ResourceType = MapResourceType(tag),
                    Route = route,
                    Description = $"Auto-scanned from Views/{relativePath}",
                    SourceFile = relativePath
                });
            }
        }

        return results;
    }

    private string ResolveResourceName(
        string? permissionKey,
        string fullOpeningTag,
        string content,
        Match match,
        string tag,
        string resourceCode)
    {
        var fromKey = ResolveLocalizedName(permissionKey);
        if (!string.IsNullOrWhiteSpace(fromKey))
        {
            return fromKey;
        }

        var legacyName = ExtractAttributeValue(fullOpeningTag, "data-permission-name");
        if (!string.IsNullOrWhiteSpace(legacyName) && !IsBrokenLocalizerName(legacyName))
        {
            return legacyName.Trim();
        }

        var localizerKey = ExtractLocalizerKey(legacyName) ?? ExtractLocalizerKey(fullOpeningTag);
        fromKey = ResolveLocalizedName(localizerKey);
        if (!string.IsNullOrWhiteSpace(fromKey))
        {
            return fromKey;
        }

        var innerText = ExtractInnerText(content, match.Index + match.Length, tag);
        var trimmedInner = innerText?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedInner) && !IsBrokenLocalizerName(trimmedInner))
        {
            return trimmedInner;
        }

        return resourceCode;
    }

    private string? ResolveLocalizedName(string? resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        var localized = _localizer[resourceKey.Trim()];
        if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
        {
            return localized.Value.Trim();
        }

        return SharedResourceNameResolver.TryResolve(_environment, resourceKey);
    }

    private static bool IsBrokenLocalizerName(string value)
    {
        return value.StartsWith("@Localizer[", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractLocalizerKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = LocalizerKeyRegex().Match(raw);
        return match.Success ? match.Groups["key"].Value.Trim() : null;
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

    [GeneratedRegex(@"@Localizer\[""(?<key>[^""]+)""\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocalizerKeyRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"@\{[\s\S]*?\}|@[\w.]+(?:\([^)]*\))?", RegexOptions.CultureInvariant)]
    private static partial Regex RazorExpressionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
