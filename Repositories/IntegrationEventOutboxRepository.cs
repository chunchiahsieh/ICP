using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.Integration;
using Microsoft.EntityFrameworkCore;

namespace ICP.Repositories;

public class IntegrationEventOutboxRepository : IIntegrationEventOutboxRepository
{
    private readonly ApplicationDbContext _db;

    public IntegrationEventOutboxRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(
        ShipInfoCaseInitiatedEvent integrationEvent,
        string payloadJson,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var entry = new IntegrationEventOutbox
        {
            Id = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            CaseType = integrationEvent.CaseType,
            HeaderKey = integrationEvent.HeaderKey,
            CaseNo = integrationEvent.CaseNo,
            PayloadJson = payloadJson,
            Status = IntegrationEventOutboxStatuses.Pending,
            RetryCount = 0
        };
        CrudAuditHelper.ApplyCreateAudit(entry, userName);
        _db.IntegrationEventOutboxes.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationEventOutbox>> GetPendingBatchAsync(
        int batchSize,
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        var size = batchSize < 1 ? 20 : batchSize;
        return await _db.IntegrationEventOutboxes
            .Where(x => x.Status == IntegrationEventOutboxStatuses.Pending && x.RetryCount < maxRetryCount)
            .OrderBy(x => x.CreateTime)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _db.IntegrationEventOutboxes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return;
        }

        entry.Status = IntegrationEventOutboxStatuses.Published;
        entry.PublishedAt = DateTime.UtcNow;
        entry.LastError = null;
        entry.UpdateTime = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid id,
        int retryCount,
        string error,
        bool permanent,
        CancellationToken cancellationToken = default)
    {
        var entry = await _db.IntegrationEventOutboxes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return;
        }

        entry.RetryCount = retryCount;
        entry.LastError = error.Length > 4000 ? error[..4000] : error;
        entry.Status = permanent ? IntegrationEventOutboxStatuses.Failed : IntegrationEventOutboxStatuses.Pending;
        entry.UpdateTime = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
