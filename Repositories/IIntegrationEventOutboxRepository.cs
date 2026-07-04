using ICP.Models.Icp;
using ICP.Models.Integration;

namespace ICP.Repositories;

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
}
