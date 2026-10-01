using ICP.Models.Ilc;

namespace ICP.Helpers;

public static class UserDisplayNameHelper
{
    public static string NormalizeAccount(string? account)
    {
        var value = account?.Trim() ?? string.Empty;
        var separator = value.LastIndexOf('\\');
        return (separator >= 0 ? value[(separator + 1)..] : value).Trim();
    }

    public static IReadOnlyDictionary<string, string> BuildDisplayNameMap(IEnumerable<UserInfoAd> users)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in users
            .Where(user => !string.IsNullOrWhiteSpace(user.TelId) && !string.IsNullOrWhiteSpace(user.DisplayName))
            .GroupBy(user => NormalizeAccount(user.TelId), StringComparer.OrdinalIgnoreCase))
        {
            if (group.Key.Length == 0) continue;
            var names = group.Select(user => user.DisplayName!.Trim()).Distinct(StringComparer.Ordinal).ToList();
            // Do not attribute an audit record to an arbitrary person when directory entries conflict.
            if (names.Count == 1) result[group.Key] = names[0];
        }
        return result;
    }

    public static string ResolveDisplayName(string? account, IReadOnlyDictionary<string, string> names)
    {
        var key = NormalizeAccount(account);
        return key.Length > 0 && names.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : account ?? string.Empty;
    }
}
