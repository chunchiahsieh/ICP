using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoHeaderSummaryDto
{
    [JsonPropertyName("shipNo")]
    public string? ShipNo { get; init; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("broker")]
    public string? Broker { get; init; }

    [JsonPropertyName("customer")]
    public string? Customer { get; init; }

    [JsonPropertyName("saDate")]
    public string? SaDate { get; init; }
}

public class ShipInfoDetailSummaryDto
{
    [JsonPropertyName("detailCount")]
    public int DetailCount { get; init; }

    [JsonPropertyName("totalQty")]
    public decimal TotalQty { get; init; }

    [JsonPropertyName("totalWeight")]
    public decimal? TotalWeight { get; init; }

    [JsonPropertyName("totalInvoiceQty")]
    public decimal? TotalInvoiceQty { get; init; }

    [JsonPropertyName("totalCarton")]
    public int? TotalCarton { get; init; }
}

public class ShipInfoCaseDrawerData
{
    [JsonPropertyName("headerKey")]
    public string HeaderKey { get; init; } = string.Empty;

    [JsonPropertyName("caseType")]
    public string CaseType { get; init; } = string.Empty;

    [JsonPropertyName("headerSummary")]
    public ShipInfoHeaderSummaryDto HeaderSummary { get; init; } = new();

    [JsonPropertyName("header")]
    public Dictionary<string, object?> Header { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("detailSummary")]
    public ShipInfoDetailSummaryDto DetailSummary { get; init; } = new();

    [JsonPropertyName("details")]
    public IReadOnlyList<Dictionary<string, object?>> Details { get; init; } = [];

    [JsonPropertyName("canSubmit")]
    public bool CanSubmit { get; init; }

    [JsonPropertyName("validationMessages")]
    public IReadOnlyList<string> ValidationMessages { get; init; } = [];
}

public class ShipInfoCaseCreateResult
{
    [JsonPropertyName("headerKey")]
    public string HeaderKey { get; init; } = string.Empty;

    [JsonPropertyName("caseType")]
    public string CaseType { get; init; } = string.Empty;

    [JsonPropertyName("depositNo")]
    public string? DepositNo { get; init; }

    [JsonPropertyName("arurNo")]
    public string? ArurNo { get; init; }

    [JsonPropertyName("newStatus")]
    public string? NewStatus { get; init; }
}

public static class ShipInfoCaseTypes
{
    public const string Deposit = "Deposit";
    public const string Arur = "ARUR";
}
