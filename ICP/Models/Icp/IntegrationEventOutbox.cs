using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("INTEGRATION_EVENT_OUTBOX")]
public class IntegrationEventOutbox
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string CaseType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string HeaderKey { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CaseNo { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreateTime { get; set; }

    [MaxLength(100)]
    public string? CreateUser { get; set; }

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}
