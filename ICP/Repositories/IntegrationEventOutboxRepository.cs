using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.Integration;
using ICP.Models.ShipInfo;
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
            Id = integrationEvent.MessageId,
            EventType = integrationEvent.EventType,
            CaseType = integrationEvent.Payload.CaseType,
            HeaderKey = integrationEvent.Payload.HeaderKey,
            CaseNo = integrationEvent.Payload.CaseNo,
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

    public async Task<bool> TryRequeueFailedAsync(
        string headerKey,
        string caseType,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(headerKey) || string.IsNullOrWhiteSpace(caseType))
        {
            return false;
        }

        var entry = await _db.IntegrationEventOutboxes
            .Where(x =>
                x.HeaderKey == headerKey
                && x.CaseType == caseType
                && x.Status == IntegrationEventOutboxStatuses.Failed)
            .OrderByDescending(x => x.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return false;
        }

        entry.Status = IntegrationEventOutboxStatuses.Pending;
        entry.RetryCount = 0;
        entry.LastError = null;
        CrudAuditHelper.ApplyUpdateAudit(entry, userName);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<string, OutboxFailedFlags>> GetFailedFlagsByHeaderKeysAsync(
        IEnumerable<string> headerKeys,
        CancellationToken cancellationToken = default)
    {
        var keys = headerKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<string, OutboxFailedFlags>(StringComparer.Ordinal);
        }

        var failedRows = await _db.IntegrationEventOutboxes
            .AsNoTracking()
            .Where(x =>
                keys.Contains(x.HeaderKey)
                && x.Status == IntegrationEventOutboxStatuses.Failed
                && (x.CaseType == ShipInfoCaseTypes.Deposit || x.CaseType == ShipInfoCaseTypes.Arur))
            .Select(x => new { x.HeaderKey, x.CaseType })
            .ToListAsync(cancellationToken);

        var result = keys.ToDictionary(
            k => k,
            _ => new OutboxFailedFlags(),
            StringComparer.Ordinal);

        foreach (var group in failedRows.GroupBy(x => x.HeaderKey, StringComparer.Ordinal))
        {
            result[group.Key] = new OutboxFailedFlags
            {
                DepositFailed = group.Any(x =>
                    string.Equals(x.CaseType, ShipInfoCaseTypes.Deposit, StringComparison.OrdinalIgnoreCase)),
                ArurFailed = group.Any(x =>
                    string.Equals(x.CaseType, ShipInfoCaseTypes.Arur, StringComparison.OrdinalIgnoreCase))
            };
        }

        return result;
    }
}
