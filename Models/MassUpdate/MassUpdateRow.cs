namespace ICP.Models.MassUpdate;

public class MassUpdateRow
{
    public int RowNumber { get; init; }
    public string InvoiceNo { get; init; } = string.Empty;
    public string? NcdrNo { get; init; }
    public string? Owner { get; init; }
    public string? EndUser { get; init; }
    public string? ArrivalNotice { get; init; }
    public string? SaDate { get; init; }
    public string? Forwarder { get; init; }
    public string? Broker { get; init; }
    public string? Eta { get; init; }
    public string? Mawb { get; init; }
    public string? Hawb { get; init; }
    public string? Flt { get; init; }
    public string? DeliveryDate { get; init; }
    public string? MdpFlag { get; init; }
    public string? ReasonForDeliveryDelay { get; init; }
    public string? DelayNotificationDate { get; init; }
}

public class MassUpdatePreviewRow
{
    public required MassUpdateRow Row { get; init; }
    public int MatchedHeaderCount { get; init; }
    public bool IsDuplicateInFile { get; init; }
    public string? DbNcdrNo { get; init; }
    public string? DbOwner { get; init; }
    public string? DbEndUser { get; init; }
    public bool IsNotFound => MatchedHeaderCount == 0;
    public bool CanSave => !IsDuplicateInFile && !IsNotFound;
}

public class MassUpdateResult
{
    public int UpdatedHeaderCount { get; init; }
    public int MatchedExcelRowCount { get; init; }
    public int NotFoundExcelRowCount { get; init; }
}
