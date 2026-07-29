using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

/// <summary>
/// 郵件群組角色對應表（Address 來自 FIESTA MailGroup）。
/// </summary>
[Table("RolesMailGroup")]
public class RoleMailGroup
{
    [Key]
    public Guid Id { get; set; }

    [Column("Address")]
    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    public bool IsEnabled { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }

    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;
}
