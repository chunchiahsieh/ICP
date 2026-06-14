using System.Text.RegularExpressions;

namespace ICP.Services;

/// <summary>
/// 從 ResourceCode 產生寫入 DB 的 ResourceName（固定繁中，供後台操作者閱讀；不走 UI 多語系）。
/// </summary>
public static partial class PermissionResourceNameResolver
{
    private static readonly Dictionary<string, string> ActionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["View"] = "查詢",
        ["Create"] = "新增",
        ["Edit"] = "編輯",
        ["Delete"] = "刪除",
        ["Disable"] = "停用",
        ["Scan"] = "掃描同步",
        ["Allow"] = "允許",
        ["Upload"] = "上傳"
    };

    public static string Resolve(IWebHostEnvironment environment, string resourceCode)
    {
        if (string.IsNullOrWhiteSpace(resourceCode))
        {
            return string.Empty;
        }

        var fromResx = SharedResourceNameResolver.TryResolve(environment, resourceCode.Trim());
        if (IsValidOperatorName(fromResx))
        {
            return fromResx!;
        }

        var legacyName = DeriveFromResourceCode(environment, resourceCode.Trim());
        if (IsValidOperatorName(legacyName))
        {
            return legacyName!;
        }

        return resourceCode.Trim();
    }

    public static bool IsCorruptedName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (name.StartsWith("@Localizer[", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.StartsWith("[\"", StringComparison.Ordinal))
        {
            return true;
        }

        if (BracketedLocalizerKeyRegex().IsMatch(name))
        {
            return true;
        }

        if (name.StartsWith("Common.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsValidOperatorName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && !IsCorruptedName(name);
    }

    private static string? DeriveFromResourceCode(IWebHostEnvironment environment, string resourceCode)
    {
        var segments = resourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        if (segments.Length >= 4 &&
            segments[0].Equals("Views", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("Shared", StringComparison.OrdinalIgnoreCase) &&
            segments[2].Equals("_SidebarNav", StringComparison.OrdinalIgnoreCase))
        {
            return segments[^1];
        }

        if (segments.Length < 4 ||
            !segments[0].Equals("Views", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var module = segments[2];
        var action = segments[^1];
        var actionLabel = ActionLabels.TryGetValue(action, out var label) ? label : action;

        string? moduleTitle = null;
        if (segments[1].Equals("Setting", StringComparison.OrdinalIgnoreCase))
        {
            moduleTitle = SharedResourceNameResolver.TryResolve(
                environment,
                $"Views.Shared._SidebarNav.Setting.{module}");
        }
        else if (segments[1].Equals("Permission", StringComparison.OrdinalIgnoreCase))
        {
            var viewName = SharedResourceNameResolver.TryResolve(
                environment,
                $"Views.Permission.{module}.View");
            moduleTitle = StripTrailingActionLabel(viewName) ?? module;
        }
        else if (segments[1].Equals("Forwarder", StringComparison.OrdinalIgnoreCase))
        {
            moduleTitle = SharedResourceNameResolver.TryResolve(
                environment,
                $"Views.Shared._SidebarNav.Forwarder.{module}");
        }

        moduleTitle ??= module;
        return $"{moduleTitle}{actionLabel}";
    }

    private static string? StripTrailingActionLabel(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return null;
        }

        foreach (var actionLabel in ActionLabels.Values)
        {
            if (viewName.EndsWith(actionLabel, StringComparison.Ordinal))
            {
                return viewName[..^actionLabel.Length];
            }
        }

        return viewName;
    }

    [GeneratedRegex(@"^\[\""(?<key>[^""]+)\""\]$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedLocalizerKeyRegex();
}
