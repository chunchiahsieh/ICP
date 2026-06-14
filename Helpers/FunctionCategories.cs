namespace ICP.Helpers;

public static class FunctionCategories
{
    public static readonly string[] All =
    [
        "ShipInfo",
        "AddDiSa",
        "MassUpdateNonNcpi",
        "MassUpdateNcpi",
        "Export",
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? controllerName)
    {
        return !string.IsNullOrWhiteSpace(controllerName) && KnownSet.Contains(controllerName);
    }
}
