namespace ICP.Helpers;

public static class BrokerCategories
{
    public static readonly string[] All =
    [
        "CustomsDataDownload",
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? controllerName)
    {
        return !string.IsNullOrWhiteSpace(controllerName) && KnownSet.Contains(controllerName);
    }
}
