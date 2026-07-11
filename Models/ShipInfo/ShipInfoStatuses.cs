namespace ICP.Models.ShipInfo;

public static class ShipInfoStatuses
{
    public const string Processing = "Processing";
    public const string Cancelled = "Cancelled";

    public const string LookupCategory = "ShipInfoStatus";

    public static IReadOnlyList<ShipInfoLookupOption> LookupOptions { get; } =
    [
        new() { Value = Processing, Text = "處理中" },
        new() { Value = Cancelled, Text = "作廢" }
    ];
}
