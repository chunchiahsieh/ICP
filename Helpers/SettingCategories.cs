namespace ICP.Helpers;

public static class SettingCategories
{
    public static readonly string[] CustomizedExcluded =
    [
        "BuCode",
        "WhCode",
        "DeliveryToList",
        "PickUpLocation",
        "EtaDelDateTable",
        "DefaultDeliveryWh",
        "Broker",
        "AirSea",
        "InvoiceType",
        "OrderType",
        "OrderPriority",
    ];

    public static readonly string[] All =
    [
        "BuCode",
        "WhCode",
        "DeliveryToList",
        "PickUpLocation",
        "EtaDelDateTable",
        "DefaultDeliveryWh",
        "Broker",
        "AirSea",
        "InvoiceType",
        "OrderType",
        "OrderPriority",
        "Customized",
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> CustomizedExcludedSet = new(CustomizedExcluded, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? controllerName)
    {
        return !string.IsNullOrWhiteSpace(controllerName) && KnownSet.Contains(controllerName);
    }

    public static bool IsInCustomizedScope(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && !CustomizedExcludedSet.Contains(category);
    }
}
