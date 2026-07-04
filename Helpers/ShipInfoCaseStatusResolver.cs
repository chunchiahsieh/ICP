using ICP.Models.ShipInfo;

namespace ICP.Helpers;

public static class ShipInfoCaseStatusResolver
{
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ShipInfoCaseStatuses.NotInitiated;
        }

        var trimmed = status.Trim();
        if (trimmed.Equals(ShipInfoCaseStatuses.NotInitiated, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("未起案", StringComparison.Ordinal))
        {
            return ShipInfoCaseStatuses.NotInitiated;
        }

        if (trimmed.Equals(ShipInfoCaseStatuses.Initiated, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("已起案", StringComparison.Ordinal))
        {
            return ShipInfoCaseStatuses.Initiated;
        }

        if (trimmed.Equals(ShipInfoCaseStatuses.Failed, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("起案失敗", StringComparison.Ordinal))
        {
            return ShipInfoCaseStatuses.Failed;
        }

        if (trimmed.Equals(ShipInfoCaseStatuses.Processing, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("處理中", StringComparison.Ordinal))
        {
            return ShipInfoCaseStatuses.Processing;
        }

        return trimmed;
    }

    public static bool CanCreateCase(string? status) =>
        Normalize(status) is ShipInfoCaseStatuses.NotInitiated or ShipInfoCaseStatuses.Failed;

    public static bool IsActionLocked(string? status) => !CanCreateCase(status);

    public static bool MatchesSearch(string normalizedCode, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (normalizedCode.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GetSearchAliases(normalizedCode).Any(alias =>
            alias.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetSearchAliases(string normalizedCode) =>
        normalizedCode switch
        {
            ShipInfoCaseStatuses.NotInitiated => ["NotInitiated", "未起案"],
            ShipInfoCaseStatuses.Initiated => ["Initiated", "已起案"],
            ShipInfoCaseStatuses.Failed => ["Failed", "起案失敗"],
            ShipInfoCaseStatuses.Processing => ["Processing", "處理中"],
            _ => [normalizedCode]
        };
}
