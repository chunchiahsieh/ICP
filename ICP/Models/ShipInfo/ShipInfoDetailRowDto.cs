namespace ICP.Models.ShipInfo;

/// <summary>ShipInfo 明細清單／編輯 API 的欄位契約（JSON fieldName 應對應此 DTO 屬性）。</summary>
public class ShipInfoDetailRowDto
{
    [ShipInfoComputed]
    public string Id { get; init; } = string.Empty;

    [ShipInfoComputed]
    public Guid RowId { get; init; }

    [ShipInfoComputed]
    public string DetailKey { get; init; } = string.Empty;

    [ShipInfoComputed]
    public string HeaderKey { get; init; } = string.Empty;

    public DateTime CreateTime { get; init; }

    public string? CreateUser { get; init; }

    public DateTime? UpdateTime { get; init; }

    public string? UpdateUser { get; init; }

    public string InvoiceNo { get; init; } = string.Empty;

    public string TetPo { get; init; } = string.Empty;

    public string? TetPoLine { get; init; }

    public double? InvoiceSeq { get; init; }

    [ShipInfoComputed]
    public double? LineNo { get; init; }

    public string? ItemNo { get; init; }

    [ShipInfoComputed]
    public string? MaterialCode { get; init; }

    public string? Description { get; init; }

    public decimal? Qty { get; init; }

    [ShipInfoComputed]
    public decimal? Quantity { get; init; }

    [ShipInfoComputed]
    public decimal? InvoiceQty { get; init; }

    public string? Uom { get; init; }

    public string? Coo { get; init; }

    public double? Price { get; init; }

    public double? Amount { get; init; }

    public string? Currency { get; init; }

    public decimal? Rate { get; init; }

    public string? PackingType { get; init; }

    public double? CartonNo { get; init; }

    [ShipInfoComputed]
    public double? Carton { get; init; }

    public double? Length { get; init; }

    public double? Width { get; init; }

    public double? Hight { get; init; }

    public decimal? GrossWeight { get; init; }

    [ShipInfoComputed]
    public decimal? Weight { get; init; }

    public double? NetWeightOfTheItem { get; init; }

    public double? DeliveryLineNo { get; init; }

    public string? Eccn { get; init; }

    public string? ElFlag { get; init; }

    public string? SdsFlag { get; init; }

    public string? Hazmat { get; init; }

    public string DepositCaseStatus { get; init; } = ShipInfoCaseStatuses.NotInitiated;

    public string ArurCaseStatus { get; init; } = ShipInfoCaseStatuses.NotInitiated;
}
