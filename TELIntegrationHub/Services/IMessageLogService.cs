using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public interface IMessageLogService
{
    Task<MessageLog> RecordReceivedAsync(
        string messageId,
        string eventType,
        string sourceSystem,
        string? correlationId,
        string payload,
        string? targetSystem = null,
        CancellationToken cancellationToken = default);

    Task MarkSuccessAsync(long id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(long id, string errorMessage, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageLog>> QueryAsync(MessageLogQuery query, CancellationToken cancellationToken = default);

    Task<MessageLog?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageLog>> GetErrorsAsync(CancellationToken cancellationToken = default);
}

public sealed class MessageLogQuery
{
    public string? SourceSystem { get; init; }

    public string? TargetSystem { get; init; }

    public string? EventType { get; init; }

    public MessageLogStatus? Status { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public int Take { get; init; } = 100;
}
