using System.ComponentModel.DataAnnotations;

namespace ICP.Models;

public class RolePermissionEditModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Role 為必填")]
    public Guid RoleId { get; set; }

    [Required(ErrorMessage = "Resource 為必填")]
    public Guid ResourceId { get; set; }

    [Required(ErrorMessage = "ActionCode 為必填")]
    [MaxLength(50)]
    public string ActionCode { get; set; } = string.Empty;

    public bool IsAllowed { get; set; } = true;

    [MaxLength(50)]
    public string? DataScope { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
