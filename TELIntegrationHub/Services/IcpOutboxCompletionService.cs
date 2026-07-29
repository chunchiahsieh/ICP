using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Data;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public interface IIcpOutboxCompletionService
{
    Task MarkCompletedAsync(Guid messageId, CancellationToken cancellationToken = default);
}

public sealed class IcpOutboxCompletionService : IIcpOutboxCompletionService
{
    private readonly IcpDbContext _db;
    private readonly ILogger<IcpOutboxCompletionService> _logger;

    public IcpOutboxCompletionService(
        IcpDbContext db,
        ILogger<IcpOutboxCompletionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task MarkCompletedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
        {
            _logger.LogWarning("Skip Outbox Completed: empty messageId.");
            return;
        }

        try
        {
            var entry = await _db.OutboxEntries
                .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

            if (entry is null)
            {
                _logger.LogWarning(
                    "ICP Outbox row not found for messageId={MessageId}; skip Completed ack.",
                    messageId);
                return;
            }

            if (string.Equals(entry.Status, IcpOutboxStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(entry.Status, IcpOutboxStatuses.Published, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ICP Outbox messageId={MessageId} status={Status}; expected Published. Skip Completed.",
                    messageId,
                    entry.Status);
                return;
            }

            entry.Status = IcpOutboxStatuses.Completed;
            entry.UpdateTime = DateTime.Now;
            entry.UpdateUser = "HUB";
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marked ICP Outbox Completed for messageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            // Do not fail Hub MessageLog Success if ICP DB ack fails; surface for ops.
            _logger.LogError(ex, "Failed to mark ICP Outbox Completed for messageId={MessageId}", messageId);
        }
    }
}
