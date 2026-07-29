using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ICP.Models.ShipInfo;

namespace ICP.Models.Icp;

[Table("ICP_DETAIL")]
public class IcpDetail : IcpAuditableEntity
{
    [Column("INVOICE_NO")]
    [MaxLength(30)]
    public string InvoiceNo { get; set; } = string.Empty;

    [Column("TET_PO")]
    [MaxLength(35)]
    public string TetPo { get; set; } = string.Empty;

    [Column("TET_PO_LINE")]
    [MaxLength(35)]
    public string? TetPoLine { get; set; }

    [Column("INVOICE_SEQ")]
    public double? InvoiceSeq { get; set; }

    [Column("ITEM_NO")]
    [MaxLength(47)]
    public string? ItemNo { get; set; }

    [Column("DESCRIPTION")]
    [MaxLength(60)]
    public string? Description { get; set; }

    [Column("QTY", TypeName = "numeric(13, 3)")]
    public decimal? Qty { get; set; }

    [Column("UOM")]
    [MaxLength(10)]
    public string? Uom { get; set; }

    [Column("COO")]
    [MaxLength(50)]
    public string? Coo { get; set; }

    [Column("PRICE")]
    public double? Price { get; set; }

    [Column("AMOUNT")]
    public double? Amount { get; set; }

    [Column("CURRENCY")]
    [MaxLength(3)]
    public string? Currency { get; set; }

    [Column("RATE", TypeName = "numeric(18, 4)")]
    public decimal? Rate { get; set; }

    [Column("PACKING_TYPE")]
    [MaxLength(50)]
    public string? PackingType { get; set; }

    [Column("CARTON_NO")]
    public double? CartonNo { get; set; }

    [Column("LENGTH")]
    public double? Length { get; set; }

    [Column("WIDTH")]
    public double? Width { get; set; }

    [Column("HIGHT")]
    public double? Hight { get; set; }

    [Column("GROSS_WEIGHT", TypeName = "numeric(6, 3)")]
    public decimal? GrossWeight { get; set; }

    [Column("NET_WEIGHT_OF_THE_ITEM")]
    public double? NetWeightOfTheItem { get; set; }

    [Column("DELIVERY_LINE_NO")]
    public double? DeliveryLineNo { get; set; }

    [Column("ECCN")]
    [MaxLength(10)]
    public string? Eccn { get; set; }

    [Column("EL_FLAG")]
    [MaxLength(5)]
    public string? ElFlag { get; set; }

    [Column("SDS_FLAG")]
    [MaxLength(5)]
    public string? SdsFlag { get; set; }

    [Column("HAZMAT")]
    [MaxLength(5)]
    public string? Hazmat { get; set; }

    [Column("DEPOSIT_CASE_STATUS")]
    [MaxLength(20)]
    public string DepositCaseStatus { get; set; } = ShipInfoCaseStatuses.NotInitiated;

    [Column("ARUR_CASE_STATUS")]
    [MaxLength(20)]
    public string ArurCaseStatus { get; set; } = ShipInfoCaseStatuses.NotInitiated;

    public IcpHeader? Header { get; set; }
}
