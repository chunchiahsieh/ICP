using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

/// <summary>
/// 角色主檔。
/// </summary>
[Table("Roles")]
public class Role
{
    /// <summary>主鍵 Id</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>角色代碼（例如 admin / manager / icp-user）</summary>
    [MaxLength(100)]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>角色名稱（例如 系統管理員）</summary>
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;

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

    /// <summary>使用者角色對應</summary>
    public ICollection<RoleTelId> RoleTelIds { get; set; } = [];

    /// <summary>部門角色對應</summary>
    public ICollection<RoleDepId> RoleDepIds { get; set; } = [];

    /// <summary>郵件群組角色對應</summary>
    public ICollection<RoleMailGroup> RoleMailGroups { get; set; } = [];

    /// <summary>角色權限對應</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
