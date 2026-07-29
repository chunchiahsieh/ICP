using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("SHIPINFO_AUDIT_LOG")]
public class ShipInfoAuditLog
{
    [Key]
    public long Id { get; set; }

    [MaxLength(20)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string EntityKey { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? HeaderKey { get; set; }

    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CaseType { get; set; }

    [MaxLength(50)]
    public string? CaseNo { get; set; }

    [MaxLength(50)]
    public string? OldStatus { get; set; }

    [MaxLength(50)]
    public string? NewStatus { get; set; }

    public DateTime ActionTime { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}
