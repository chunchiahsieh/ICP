using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Data;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public sealed class MessageLogService : IMessageLogService
{
    private readonly HubDbContext _db;

    public MessageLogService(HubDbContext db) => _db = db;

    public async Task<MessageLog> RecordReceivedAsync(
        string messageId,
        string eventType,
        string sourceSystem,
        string? correlationId,
        string payload,
        string? targetSystem = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new MessageLog
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            EventType = eventType,
            SourceSystem = sourceSystem,
            TargetSystem = targetSystem,
            Payload = payload,
            Status = MessageLogStatus.Processing,
            RetryCount = 0,
            ReceivedAt = now,
            CreateTime = now,
            UpdateTime = now
        };

        _db.MessageLogs.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task MarkSuccessAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MessageLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return;

        var now = DateTime.UtcNow;
        entity.Status = MessageLogStatus.Success;
        entity.ProcessedAt = now;
        entity.UpdateTime = now;
        entity.ErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(long id, string errorMessage, CancellationToken cancellationToken = default)
    {
        var entity = await _db.MessageLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return;

        var now = DateTime.UtcNow;
        entity.Status = MessageLogStatus.Failed;
        entity.ErrorMessage = Truncate(errorMessage, 2000);
        entity.ProcessedAt = now;
        entity.UpdateTime = now;
        entity.RetryCount += 1;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MessageLog>> QueryAsync(
        MessageLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var q = _db.MessageLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SourceSystem))
            q = q.Where(x => x.SourceSystem == query.SourceSystem);
        if (!string.IsNullOrWhiteSpace(query.TargetSystem))
            q = q.Where(x => x.TargetSystem == query.TargetSystem);
        if (!string.IsNullOrWhiteSpace(query.EventType))
            q = q.Where(x => x.EventType == query.EventType);
        if (query.Status is not null)
            q = q.Where(x => x.Status == query.Status);
        if (query.From is not null)
            q = q.Where(x => x.ReceivedAt >= query.From);
        if (query.To is not null)
            q = q.Where(x => x.ReceivedAt <= query.To);

        var take = query.Take <= 0 ? 100 : Math.Min(query.Take, 500);
        return await q.OrderByDescending(x => x.ReceivedAt).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<MessageLog?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return await _db.MessageLogs.AsNoTracking()
            .Where(x => x.MessageId == messageId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MessageLog>> GetErrorsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.MessageLogs.AsNoTracking()
            .Where(x => x.Status == MessageLogStatus.Failed || x.Status == MessageLogStatus.DeadLetter)
            .OrderByDescending(x => x.ReceivedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
