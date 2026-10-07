using System.ComponentModel.DataAnnotations;

namespace ICP.Models.Icp;

public sealed class NotificationMailLog
{
    [Key]
    public Guid Id { get; set; }
    [MaxLength(40)]
    public string MailType { get; set; } = string.Empty;
    [MaxLength(200)]
    public string EventKey { get; set; } = string.Empty;
    public string MailTo { get; set; } = string.Empty;
    public string CcTo { get; set; } = string.Empty;
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    [MaxLength(20)]
    public string State { get; set; } = "Sending";
    public DateTime CreatedUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
}
