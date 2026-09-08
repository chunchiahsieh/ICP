using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("DELETE_LOG")]
public class DeleteLog
{
    [Key]
    public long Id { get; set; }

    [MaxLength(100)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SourceTable { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Action { get; set; } = "Delete";

    public string DataJson { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}
