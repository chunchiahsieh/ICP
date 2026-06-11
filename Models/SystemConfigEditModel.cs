using System.ComponentModel.DataAnnotations;

namespace ICP.Models;

public class SystemConfigEditModel
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [MaxLength(50)]
    public string? FunctionCode { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Key1 { get; set; }

    [MaxLength(100)]
    public string? Key2 { get; set; }

    [MaxLength(1000)]
    public string? Value1 { get; set; }

    [MaxLength(1000)]
    public string? Value2 { get; set; }

    [MaxLength(1000)]
    public string? Value3 { get; set; }

    [MaxLength(1000)]
    public string? Value4 { get; set; }

    [MaxLength(1000)]
    public string? Value5 { get; set; }

    [MaxLength(1000)]
    public string? Value6 { get; set; }
}
