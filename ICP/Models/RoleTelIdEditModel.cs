using System.ComponentModel.DataAnnotations;

namespace ICP.Models;

public class RoleTelIdEditModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "TELID 為必填")]
    [MaxLength(50)]
    public string TelId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role 為必填")]
    public Guid RoleId { get; set; }

    public bool IsEnabled { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }
}
