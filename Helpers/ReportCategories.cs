namespace ICP.Helpers;

public static class ReportCategories
{
    public static readonly string[] All =
    [
        "ShippingReport",
        "CompareIcpVsArUr",
        "MassDataReport",
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? controllerName)
    {
        return !string.IsNullOrWhiteSpace(controllerName) && KnownSet.Contains(controllerName);
    }
}
