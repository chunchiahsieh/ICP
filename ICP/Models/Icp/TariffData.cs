using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("TariffData")]
public class TariffData
{
    [Key]
    public long Id { get; set; }

    [MaxLength(50)]
    public string MAWB { get; set; } = string.Empty;

    [MaxLength(50)]
    public string HAWB { get; set; } = string.Empty;

    public DateOnly ImportDate { get; set; }

    public DateOnly DeclarationDate { get; set; }

    public DateOnly ReleaseDate { get; set; }

    [MaxLength(50)]
    public string LineNo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PartNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PONumber { get; set; }

    [MaxLength(200)]
    public string DescriptionOfGoods { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Quantity { get; set; } = string.Empty;

    [MaxLength(50)]
    public string UOM { get; set; } = string.Empty;

    [MaxLength(50)]
    public string NetWeightKg { get; set; } = string.Empty;

    [MaxLength(50)]
    public string UnitValue { get; set; } = string.Empty;

    [MaxLength(50)]
    public string HTSNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string COO { get; set; } = string.Empty;

    [MaxLength(50)]
    public string DutyRate { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DutyTreatment { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PermitNo1 { get; set; }

    [MaxLength(100)]
    public string? PermitItem1 { get; set; }

    [MaxLength(100)]
    public string? PermitNo2 { get; set; }

    [MaxLength(100)]
    public string? PermitItem2 { get; set; }

    [MaxLength(100)]
    public string? PermitNo3 { get; set; }

    [MaxLength(100)]
    public string? PermitItem3 { get; set; }

    [MaxLength(100)]
    public string EntryNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Mode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PortOfDeparture { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FlightNo { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Shipper { get; set; }

    [MaxLength(50)]
    public string TermsOfTrade { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Currency { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ExchangeRate { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? CIFValue { get; set; }

    [MaxLength(50)]
    public string? FreightCharge { get; set; }

    [MaxLength(50)]
    public string? TotalPieces { get; set; }

    [MaxLength(50)]
    public string? GrossWeightKg { get; set; }

    [MaxLength(200)]
    public string? Broker { get; set; }

    [MaxLength(50)]
    public string AirSea { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal? TotalAmountForeignCurrency { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? TotalAmountTWD { get; set; }

    [MaxLength(50)]
    public string DeclarationAmountTWD { get; set; } = string.Empty;

    public DateOnly CreateDate { get; set; }

    [MaxLength(500)]
    public string? DeclarationFile { get; set; }

    [MaxLength(50)]
    public string? Cost { get; set; }

    public Guid? ImportBatchId { get; set; }

    [MaxLength(255)]
    public string? ImportFileName { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(50)]
    public string CreateUser { get; set; } = string.Empty;

    public DateTime? UpdateTime { get; set; }

    [MaxLength(50)]
    public string? UpdateUser { get; set; }
}
