using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

/// <summary>
/// 系統資源（選單、按鈕、欄位、API 等）定義。
/// </summary>
[Table("Resources")]
public class Resource
{
    /// <summary>主鍵</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>父資源 Id，用於建立樹狀選單</summary>
    public Guid? ParentId { get; set; }

    /// <summary>系統代碼（例如 ICP / MES / ERP）</summary>
    [MaxLength(50)]
    public string SystemCode { get; set; } = string.Empty;

    /// <summary>模組代碼（例如 user / order）</summary>
    [MaxLength(50)]
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>資源代碼（例如 icp.permission.user.create）</summary>
    [MaxLength(200)]
    public string ResourceCode { get; set; } = string.Empty;

    /// <summary>資源名稱（例如 新增使用者）</summary>
    [MaxLength(200)]
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>資源類型（Page / Button / Field / API / Menu）</summary>
    [MaxLength(50)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>MVC 路由</summary>
    [MaxLength(500)]
    public string? Route { get; set; }

    /// <summary>選單圖示</summary>
    [MaxLength(100)]
    public string? Icon { get; set; }

    /// <summary>排序</summary>
    public int Sort { get; set; }

    /// <summary>是否在選單顯示</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>是否啟用（false 表示停用）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>備註</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>建立時間</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>建立者</summary>
    [MaxLength(100)]
    public string? CreateUser { get; set; }

    /// <summary>更新時間</summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新者</summary>
    [MaxLength(100)]
    public string? UpdateUser { get; set; }

    /// <summary>父資源</summary>
    [ForeignKey(nameof(ParentId))]
    public Resource? Parent { get; set; }

    /// <summary>子資源</summary>
    public ICollection<Resource> Children { get; set; } = [];

    /// <summary>角色權限對應</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
