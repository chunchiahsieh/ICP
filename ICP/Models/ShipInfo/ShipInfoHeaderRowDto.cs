namespace ICP.Models.ShipInfo;

/// <summary>ShipInfo 表頭清單／編輯 API 的欄位契約（JSON fieldName 應對應此 DTO 屬性）。</summary>
public class ShipInfoHeaderRowDto
{
    [ShipInfoComputed]
    public string Id { get; init; } = string.Empty;

    [ShipInfoComputed]
    public Guid RowId { get; init; }

    [ShipInfoComputed]
    public string HeaderKey { get; init; } = string.Empty;

    [ShipInfoComputed]
    public string HeaderRowKey { get; init; } = string.Empty;

    public DateTime CreateTime { get; init; }

    public string? CreateUser { get; init; }

    public DateTime? UpdateTime { get; init; }

    public string? UpdateUser { get; init; }

    public string? CreateDate { get; init; }

    public string? Status { get; init; }

    public string? SaDate { get; init; }

    [ShipInfoComputed]
    public string? SaDateFrom { get; init; }

    public string InvoiceNo { get; init; } = string.Empty;

    public string? Forwarder { get; init; }

    public string? Broker { get; init; }

    public string? Etd { get; init; }

    public string? Eta { get; init; }

    [ShipInfoComputed]
    public string? EtaFrom { get; init; }

    public string? InvoiceDate { get; init; }

    public string? Mawb { get; init; }

    public string? Hawb { get; init; }

    public string? Flt { get; init; }

    [ShipInfoComputed]
    public string? Flight { get; init; }

    public string? Freight { get; init; }

    public string? DestinationPort { get; init; }

    public string? DestinationCountry { get; init; }

    public string? Warehouse { get; init; }

    public string? InvoiceType { get; init; }

    public string? Incoterms { get; init; }

    public string? OrderType { get; init; }

    public string? DeliveryDate { get; init; }

    public string? DeliveryTo { get; init; }

    public string? Bu { get; init; }

    public string TetPo { get; init; } = string.Empty;

    [ShipInfoComputed]
    public string ShipNo { get; init; } = string.Empty;

    public int? OrderPriority { get; init; }

    public string? MdpFlag { get; init; }

    public double? TotalCartons { get; init; }

    public string? NcdrNo { get; init; }

    public string? NcdrRequestor { get; init; }

    public string? EndUserCode { get; init; }

    public string? EndUser { get; init; }

    [ShipInfoComputed]
    public string? Customer { get; init; }

    public string? RtNo { get; init; }

    [ShipInfoComputed]
    public string? ArurNo { get; init; }

    public string? Receiver { get; init; }

    public string? Owner { get; init; }

    public string? MachineNo { get; init; }

    public string? MachineType { get; init; }

    public string? ShipReason { get; init; }

    public string? Forklift { get; init; }

    public string? MovingLabor { get; init; }

    public string? CarMethod { get; init; }

    public string? ArriveTime { get; init; }

    public string? WasteDisposal { get; init; }

    public string? DriverDetails { get; init; }

    public string? OrderReason { get; init; }

    public string? ArrivalNoticeFlag { get; init; }

    public string? ArrivalNotice { get; init; }

    public string? ReasonForDeliveryDelay { get; init; }

    public string? DelayNotificationDate { get; init; }

    public string? DeliveryNo { get; init; }

    public string? SoldToPartyCode { get; init; }

    public string? SoldToParty { get; init; }

    public string? ShipToPartyCode { get; init; }

    public string? ShipToParty { get; init; }

    public string? ShipToPartyAddress { get; init; }

    public string? EmgFlight { get; init; }

    public string? WbsElement { get; init; }

    public string? Deposit { get; init; }

    [ShipInfoComputed]
    public string? DepositNo { get; init; }

    public string DepositCaseStatus { get; init; } = ShipInfoCaseStatuses.NotInitiated;

    public string ArurCaseStatus { get; init; } = ShipInfoCaseStatuses.NotInitiated;

    /// <summary>True when latest Deposit Outbox is Failed (enables ShipInfo resend).</summary>
    [ShipInfoComputed]
    public bool DepositOutboxFailed { get; init; }

    /// <summary>True when latest ARUR Outbox is Failed (enables ShipInfo resend).</summary>
    [ShipInfoComputed]
    public bool ArurOutboxFailed { get; init; }

    public string? SapRemarks { get; init; }

    public string? Notes { get; init; }

    [ShipInfoComputed]
    public string? Remark { get; init; }

    public string? Cancellation { get; init; }

    public string? ReasonForCancellation { get; init; }

    public string? AttachedFile { get; init; }
}
