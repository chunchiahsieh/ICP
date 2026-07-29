using System.ComponentModel.DataAnnotations;

namespace ICP.Models;

public class RoleEditModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "RoleCode 為必填")]
    [MaxLength(100)]
    public string RoleCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "RoleName 為必填")]
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }
}
