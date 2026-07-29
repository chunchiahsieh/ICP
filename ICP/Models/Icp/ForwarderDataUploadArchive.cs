using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("ForwarderDataUploadArchive")]
public class ForwarderDataUploadArchive
{
    [Key]
    public long Id { get; set; }

    public long SourceId { get; set; }

    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CustomerReference { get; set; }

    [MaxLength(100)]
    public string? MaterialCode { get; set; }

    [MaxLength(500)]
    public string? OrderMaterialName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? Quantity { get; set; }

    [MaxLength(100)]
    public string? PortOfLoading { get; set; }

    [MaxLength(300)]
    public string? ShipToName { get; set; }

    public string? ShipToAddress { get; set; }

    [MaxLength(100)]
    public string? ShipToPartyCountryCode { get; set; }

    [MaxLength(50)]
    public string? ShipToPortCode { get; set; }

    [MaxLength(100)]
    public string? FreightCharge { get; set; }

    public DateTime? ConfirmedCustomDate { get; set; }

    [MaxLength(50)]
    public string? Hawb { get; set; }

    [MaxLength(50)]
    public string? Mawb { get; set; }

    public DateTime? Etd { get; set; }

    public DateTime? Eta { get; set; }

    [MaxLength(50)]
    public string? Flight1 { get; set; }

    [MaxLength(50)]
    public string? Flight2 { get; set; }

    [MaxLength(50)]
    public string? Cb { get; set; }

    [MaxLength(100)]
    public string? Action { get; set; }

    [MaxLength(50)]
    public string? Mdp { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(50)]
    public string CreateUser { get; set; } = string.Empty;

    public DateTime? UpdateTime { get; set; }

    [MaxLength(50)]
    public string? UpdateUser { get; set; }

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public DateTime RemovedTime { get; set; }

    [MaxLength(50)]
    public string RemovedUser { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ReplacedByFilePath { get; set; } = string.Empty;

    public static ForwarderDataUploadArchive FromEntity(
        ForwarderDataUpload source,
        DateTime removedTime,
        string removedUser,
        string replacedByFilePath) =>
        new()
        {
            SourceId = source.Id,
            Type = source.Type,
            InvoiceNo = source.InvoiceNo,
            CustomerReference = source.CustomerReference,
            MaterialCode = source.MaterialCode,
            OrderMaterialName = source.OrderMaterialName,
            Quantity = source.Quantity,
            PortOfLoading = source.PortOfLoading,
            ShipToName = source.ShipToName,
            ShipToAddress = source.ShipToAddress,
            ShipToPartyCountryCode = source.ShipToPartyCountryCode,
            ShipToPortCode = source.ShipToPortCode,
            FreightCharge = source.FreightCharge,
            ConfirmedCustomDate = source.ConfirmedCustomDate,
            Hawb = source.Hawb,
            Mawb = source.Mawb,
            Etd = source.Etd,
            Eta = source.Eta,
            Flight1 = source.Flight1,
            Flight2 = source.Flight2,
            Cb = source.Cb,
            Action = source.Action,
            Mdp = source.Mdp,
            CreateTime = source.CreateTime,
            CreateUser = source.CreateUser,
            UpdateTime = source.UpdateTime,
            UpdateUser = source.UpdateUser,
            FilePath = source.FilePath,
            RemovedTime = removedTime,
            RemovedUser = removedUser,
            ReplacedByFilePath = replacedByFilePath
        };
}
