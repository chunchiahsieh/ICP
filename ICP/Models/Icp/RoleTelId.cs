using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

/// <summary>
/// 使用者角色對應表（對應 User_Info_AD.TELID）。
/// </summary>
[Table("RolesTELID")]
public class RoleTelId
{
    /// <summary>主鍵 Id</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>使用者工號 / TELID（對應 User_Info_AD.TELID）</summary>
    [Column("TELID")]
    [MaxLength(50)]
    public string TelId { get; set; } = string.Empty;

    /// <summary>對應角色 Id（對應 Roles.Id）</summary>
    public Guid RoleId { get; set; }

    /// <summary>是否啟用（true=啟用，false=停用）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>備註</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>建立時間</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>建立人員</summary>
    [MaxLength(100)]
    public string? CreateUser { get; set; }

    /// <summary>更新時間</summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新人員</summary>
    [MaxLength(100)]
    public string? UpdateUser { get; set; }

    /// <summary>對應角色</summary>
    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;
}
