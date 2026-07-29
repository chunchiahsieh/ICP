using ICP.Models.Icp;
using ICP.Models.Integration;

namespace ICP.Repositories;

public sealed class OutboxFailedFlags
{
    public bool DepositFailed { get; init; }

    public bool ArurFailed { get; init; }
}

public interface IIntegrationEventOutboxRepository
{
    Task EnqueueAsync(
        ShipInfoCaseInitiatedEvent integrationEvent,
        string payloadJson,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationEventOutbox>> GetPendingBatchAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid id, int retryCount, string error, bool permanent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the latest Failed outbox row for headerKey+caseType to Pending for Worker republish.
    /// </summary>
    Task<bool> TryRequeueFailedAsync(
        string headerKey,
        string caseType,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, OutboxFailedFlags>> GetFailedFlagsByHeaderKeysAsync(
        IEnumerable<string> headerKeys,
        CancellationToken cancellationToken = default);
}
