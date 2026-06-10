using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("SystemConfigs")]
public class SystemConfig
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FunctionCode { get; set; }

    [MaxLength(100)]
    public string Key1 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Key2 { get; set; } = string.Empty;

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

    public bool IsDeleted { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}
