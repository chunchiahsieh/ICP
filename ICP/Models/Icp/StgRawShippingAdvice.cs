using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("StgRaw_ShippingAdvice")]
public class StgRawShippingAdvice
{
    public Guid RunId { get; set; }

    public string FileCode { get; set; } = string.Empty;

    public string? SourceFileName { get; set; }

    public byte[]? SourceFileHash { get; set; }

    public DateTime? SourceFileDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string? InvoiceNo { get; set; }

    public string? Forwarder { get; set; }

    public string? Etd { get; set; }

    public string? Eta { get; set; }

    public string? InvoiceDate { get; set; }

    public string? Mawb { get; set; }

    public string? Hawb { get; set; }

    public string? Flt { get; set; }

    public string? DestinationPort { get; set; }

    public string? DestinationCountry { get; set; }

    public string? InvoiceType { get; set; }

    public string? Incoterms { get; set; }

    public string? Bu { get; set; }

    public string? PoNo { get; set; }

    public string? PoLine { get; set; }

    public int? OrderPriority { get; set; }

    public double? InvoiceSeq { get; set; }

    public string? ItemNo { get; set; }

    public string? Description { get; set; }

    public decimal? Qty { get; set; }

    public string? Uom { get; set; }

    public string? Coo { get; set; }

    public double? Price { get; set; }

    public double? Amount { get; set; }

    public string? Currency { get; set; }

    public string? PackingType { get; set; }

    public double? CartonNo { get; set; }

    public double? Length { get; set; }

    public double? Width { get; set; }

    public double? Hight { get; set; }

    public decimal? GrossWeight { get; set; }

    public double? TotalCartons { get; set; }

    public double? NetWeightOfTheItem { get; set; }

    public string? NcdrNo { get; set; }

    public string? EndUserCode { get; set; }

    public string? EndUser { get; set; }

    public string? MachineNo { get; set; }

    public string? MachineType { get; set; }

    public string? ShipReason { get; set; }

    public string? DeliveryNo { get; set; }

    public double? DeliveryLineNo { get; set; }

    public string? SoldToPartyCode { get; set; }

    public string? SoldToParty { get; set; }

    public string? ShipToPartyCode { get; set; }

    public string? ShipToParty { get; set; }

    public string? ShipToPartyAddress { get; set; }

    public string? Hazmat { get; set; }

    public string? WbsElement { get; set; }

    public string? SapRemarks { get; set; }
}
