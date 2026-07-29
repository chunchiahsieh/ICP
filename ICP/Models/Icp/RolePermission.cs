using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

/// <summary>
/// 角色權限對應表。
/// </summary>
[Table("RolePermissions")]
public class RolePermission
{
    /// <summary>主鍵 Id</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>角色 Id（對應 Roles.Id）</summary>
    public Guid RoleId { get; set; }

    /// <summary>資源 Id（對應 Resources.Id）</summary>
    public Guid ResourceId { get; set; }

    /// <summary>動作代碼（例如 View / Create / Delete；Menu、Menu Category 為 Allow）</summary>
    [MaxLength(50)]
    public string ActionCode { get; set; } = string.Empty;

    /// <summary>是否允許（true=允許，false=禁止）</summary>
    public bool IsAllowed { get; set; } = true;

    /// <summary>資料範圍（例如 ALL / DEPARTMENT / SELF）</summary>
    [MaxLength(50)]
    public string? DataScope { get; set; }

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

    /// <summary>對應資源</summary>
    [ForeignKey(nameof(ResourceId))]
    public Resource Resource { get; set; } = null!;
}
