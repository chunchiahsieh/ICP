namespace TEL.IntegrationHub.Models;

public class MessageLog
{
    public long Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public string? TargetSystem { get; set; }

    public string Payload { get; set; } = string.Empty;

    public MessageLogStatus Status { get; set; } = MessageLogStatus.Pending;

    public int RetryCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
