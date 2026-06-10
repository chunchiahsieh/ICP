namespace ICP.Helpers;

public static class SettingCategories
{
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
        "Forklift",
        "WasteDisposal",
        "DriverDetails",
        "Deposit",
        "Cancellation"
    ];

    private static readonly HashSet<string> KnownSet = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string? controllerName)
    {
        return !string.IsNullOrWhiteSpace(controllerName) && KnownSet.Contains(controllerName);
    }
}
