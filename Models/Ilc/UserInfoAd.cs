using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Ilc;

[Table("User_Info_AD")]
public class UserInfoAd
{
    [Key]
    [Column("keyID")]
    public int KeyId { get; set; }

    [Column("DepName")]
    [MaxLength(50)]
    public string? DepName { get; set; }

    [Column("UserName")]
    [MaxLength(50)]
    public string? UserName { get; set; }

    [Column("TELID")]
    [MaxLength(50)]
    public string? TelId { get; set; }

    [Column("EmailAddress")]
    [MaxLength(200)]
    public string? EmailAddress { get; set; }

    [Column("DisplayName")]
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [Column("DepID")]
    [MaxLength(50)]
    public string? DepId { get; set; }

    [Column("DepName2")]
    [MaxLength(50)]
    public string? DepName2 { get; set; }

    [Column("Create_Date")]
    [MaxLength(50)]
    public string CreateDate { get; set; } = string.Empty;
}
