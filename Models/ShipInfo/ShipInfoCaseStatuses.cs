namespace ICP.Models.ShipInfo;

public static class ShipInfoCaseStatuses
{
    public const string NotInitiated = nameof(ShipInfoCaseStatus.NotInitiated);
    public const string Initiated = nameof(ShipInfoCaseStatus.Initiated);
    public const string Failed = nameof(ShipInfoCaseStatus.Failed);
    public const string Processing = nameof(ShipInfoCaseStatus.Processing);

    public const string LookupCategory = "ShipInfoCaseStatus";
    public const string DepositCaseStatusCategory = "DepositCaseStatus";
    public const string ArurCaseStatusCategory = "ArurCaseStatus";

    public static readonly ShipInfoCaseStatus[] LookupOrder =
    [
        ShipInfoCaseStatus.NotInitiated,
        ShipInfoCaseStatus.Failed,
        ShipInfoCaseStatus.Processing,
        ShipInfoCaseStatus.Initiated
    ];

    public static string ToCode(ShipInfoCaseStatus status) => status.ToString();

    public static bool IsLookupCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        var normalized = category.Trim();
        return normalized.Equals(LookupCategory, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(DepositCaseStatusCategory, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(ArurCaseStatusCategory, StringComparison.OrdinalIgnoreCase);
    }
}
