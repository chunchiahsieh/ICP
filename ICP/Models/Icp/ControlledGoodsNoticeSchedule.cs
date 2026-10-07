using System.ComponentModel.DataAnnotations;

namespace ICP.Models.Icp;

public sealed class ControlledGoodsNoticeSchedule
{
    [Key] public Guid Id { get; set; }
    public Guid HeaderId { get; set; }
    [MaxLength(30)] public string InvoiceNo { get; set; } = string.Empty;
    [MaxLength(10)] public string Eta { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    [MaxLength(20)] public string State { get; set; } = "Pending";
    public DateTime CreatedUtc { get; set; }
    [MaxLength(100)] public string? CreatedUser { get; set; }
    public DateTime? SendingUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    [MaxLength(100)] public string? CancelledUser { get; set; }
}
