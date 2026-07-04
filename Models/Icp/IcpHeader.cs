using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("ICP_HEADER")]
public class IcpHeader : IcpAuditableEntity
{
    [Column("CREATE_DATE")]
    [MaxLength(20)]
    public string? CreateDate { get; set; }

    [Column("STATUS")]
    [MaxLength(200)]
    public string? Status { get; set; }

    [Column("SA_DATE")]
    [MaxLength(10)]
    public string? SaDate { get; set; }

    [Column("INVOICE_NO")]
    [MaxLength(30)]
    public string InvoiceNo { get; set; } = string.Empty;

    [Column("FORWARDER")]
    [MaxLength(50)]
    public string? Forwarder { get; set; }

    [Column("BROKER")]
    [MaxLength(30)]
    public string? Broker { get; set; }

    [Column("ETD")]
    [MaxLength(10)]
    public string? Etd { get; set; }

    [Column("ETA")]
    [MaxLength(10)]
    public string? Eta { get; set; }

    [Column("INVOICE_DATE")]
    [MaxLength(10)]
    public string? InvoiceDate { get; set; }

    [Column("MAWB")]
    [MaxLength(20)]
    public string? Mawb { get; set; }

    [Column("HAWB")]
    [MaxLength(20)]
    public string? Hawb { get; set; }

    [Column("FLT")]
    [MaxLength(20)]
    public string? Flt { get; set; }

    [Column("FREIGHT")]
    [MaxLength(10)]
    public string? Freight { get; set; }

    [Column("DESTINATION_PORT")]
    [MaxLength(10)]
    public string? DestinationPort { get; set; }

    [Column("DESTINATION_COUNTRY")]
    [MaxLength(3)]
    public string? DestinationCountry { get; set; }

    [Column("WAREHOUSE")]
    [MaxLength(20)]
    public string? Warehouse { get; set; }

    [Column("INVOICE_TYPE")]
    [MaxLength(10)]
    public string? InvoiceType { get; set; }

    [Column("INCOTERMS")]
    [MaxLength(20)]
    public string? Incoterms { get; set; }

    [Column("ORDER_TYPE")]
    [MaxLength(20)]
    public string? OrderType { get; set; }

    [Column("DELIVERY_DATE")]
    [MaxLength(10)]
    public string? DeliveryDate { get; set; }

    [Column("DELIVERY_TO")]
    [MaxLength(20)]
    public string? DeliveryTo { get; set; }

    [Column("BU")]
    [MaxLength(40)]
    public string? Bu { get; set; }

    [Column("TET_PO")]
    [MaxLength(35)]
    public string TetPo { get; set; } = string.Empty;

    [Column("ORDER_PRIORITY")]
    public int? OrderPriority { get; set; }

    [Column("MDP_FLAG")]
    [MaxLength(5)]
    public string? MdpFlag { get; set; }

    [Column("TOTAL_CARTONS")]
    public double? TotalCartons { get; set; }

    [Column("NCDR_NO")]
    [MaxLength(60)]
    public string? NcdrNo { get; set; }

    [Column("NCDR_REQUESTOR")]
    [MaxLength(40)]
    public string? NcdrRequestor { get; set; }

    [Column("END_USER_CODE")]
    [MaxLength(30)]
    public string? EndUserCode { get; set; }

    [Column("END_USER")]
    [MaxLength(100)]
    public string? EndUser { get; set; }

    [Column("RT_NO")]
    [MaxLength(30)]
    public string? RtNo { get; set; }

    [Column("RECEIVER")]
    [MaxLength(200)]
    public string? Receiver { get; set; }

    [Column("OWNER")]
    [MaxLength(50)]
    public string? Owner { get; set; }

    [Column("MACHINE_NO")]
    [MaxLength(50)]
    public string? MachineNo { get; set; }

    [Column("MACHINE_TYPE")]
    [MaxLength(50)]
    public string? MachineType { get; set; }

    [Column("SHIP_REASON")]
    [MaxLength(50)]
    public string? ShipReason { get; set; }

    [Column("FORKLIFT")]
    [MaxLength(50)]
    public string? Forklift { get; set; }

    [Column("MOVING_LABOR")]
    [MaxLength(50)]
    public string? MovingLabor { get; set; }

    [Column("CAR_METHOD")]
    [MaxLength(50)]
    public string? CarMethod { get; set; }

    [Column("ARRIVE_TIME")]
    [MaxLength(50)]
    public string? ArriveTime { get; set; }

    [Column("WASTE_DISPOSAL")]
    [MaxLength(50)]
    public string? WasteDisposal { get; set; }

    [Column("DRIVER_DETAILS")]
    [MaxLength(50)]
    public string? DriverDetails { get; set; }

    [Column("ORDER_REASON")]
    [MaxLength(50)]
    public string? OrderReason { get; set; }

    [Column("ARRIVAL_NOTICE_FLAG")]
    [MaxLength(5)]
    public string? ArrivalNoticeFlag { get; set; }

    [Column("ARRIVAL_NOTICE")]
    [MaxLength(100)]
    public string? ArrivalNotice { get; set; }

    [Column("REASON_FOR_DELIVERY_DELAY")]
    [MaxLength(200)]
    public string? ReasonForDeliveryDelay { get; set; }

    [Column("DELAY_NOTIFICATION_DATE")]
    [MaxLength(10)]
    public string? DelayNotificationDate { get; set; }

    [Column("DELIVERY_NO")]
    [MaxLength(30)]
    public string? DeliveryNo { get; set; }

    [Column("SOLD_TO_PARTY_CODE")]
    [MaxLength(30)]
    public string? SoldToPartyCode { get; set; }

    [Column("SOLD_TO_PARTY")]
    [MaxLength(100)]
    public string? SoldToParty { get; set; }

    [Column("SHIP_TO_PARTY_CODE")]
    [MaxLength(30)]
    public string? ShipToPartyCode { get; set; }

    [Column("SHIP_TO_PARTY")]
    [MaxLength(100)]
    public string? ShipToParty { get; set; }

    [Column("SHIP_TO_PARTY_ADDRESS")]
    [MaxLength(200)]
    public string? ShipToPartyAddress { get; set; }

    [Column("EMG_FLIGHT")]
    [MaxLength(5)]
    public string? EmgFlight { get; set; }

    [Column("WBS_ELEMENT")]
    [MaxLength(30)]
    public string? WbsElement { get; set; }

    [Column("DEPOSIT")]
    [MaxLength(10)]
    public string? Deposit { get; set; }

    [Column("SAP_REMARKS")]
    [MaxLength(1000)]
    public string? SapRemarks { get; set; }

    [Column("NOTES")]
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Column("CANCELLATION")]
    [MaxLength(10)]
    public string? Cancellation { get; set; }

    [Column("REASON_FOR_CANCELLATION")]
    [MaxLength(200)]
    public string? ReasonForCancellation { get; set; }

    [Column("ATTACHED_FILE")]
    [MaxLength(1000)]
    public string? AttachedFile { get; set; }

    public ICollection<IcpDetail> Details { get; set; } = [];
}
