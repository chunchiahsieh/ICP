using System.ComponentModel.DataAnnotations;

namespace ICP.Models.Icp;

public abstract class IcpAuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}
