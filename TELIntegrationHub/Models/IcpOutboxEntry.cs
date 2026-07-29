using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TEL.IntegrationHub.Models;

/// <summary>Thin mapping of ICP INTEGRATION_EVENT_OUTBOX for completion ack only.</summary>
[Table("INTEGRATION_EVENT_OUTBOX")]
public class IcpOutboxEntry
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public DateTime? UpdateTime { get; set; }

    [MaxLength(100)]
    public string? UpdateUser { get; set; }
}

public static class IcpOutboxStatuses
{
    public const string Published = "Published";
    public const string Completed = "Completed";
}
