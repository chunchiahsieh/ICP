using System.ComponentModel.DataAnnotations;

namespace ICP.Models;

public class SystemConfigEditModel
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Key1 { get; set; }

    [MaxLength(1000)]
    public string? Value1 { get; set; }
}
