using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Data;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public interface IIcpOutboxCompletionService
{
    Task MarkCompletedAsync(Guid messageId, string? actualCaseNo = null, CancellationToken cancellationToken = default);

    /// <returns>True when Outbox is Failed (or already Failed/Completed). False when the row cannot be updated.</returns>
    Task<bool> MarkArurFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
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

    public async Task MarkCompletedAsync(Guid messageId, string? actualCaseNo = null, CancellationToken cancellationToken = default)
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
            await UpdateCaseStatusAsync(entry, "Initiated", actualCaseNo ?? entry.CaseNo, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Marked ICP Outbox Completed for messageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            // Do not fail Hub MessageLog Success if ICP DB ack fails; surface for ops.
            _logger.LogError(ex, "Failed to mark ICP Outbox Completed for messageId={MessageId}", messageId);
        }
    }

    public async Task<bool> MarkArurFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (messageId == Guid.Empty)
        {
            _logger.LogWarning("Skip Outbox Failed: empty messageId.");
            return false;
        }

        var entry = await _db.OutboxEntries.FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (entry is null)
        {
            _logger.LogWarning("ICP Outbox row not found for failed ARUR messageId={MessageId}", messageId);
            return false;
        }

        if (string.Equals(entry.Status, IcpOutboxStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Skip Outbox Failed: messageId={MessageId} is already Completed.",
                messageId);
            return true;
        }

        if (string.Equals(entry.Status, IcpOutboxStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        entry.Status = IcpOutboxStatuses.Failed;
        entry.LastError = error.Length > 4000 ? error[..4000] : error;
        entry.UpdateTime = DateTime.Now;
        entry.UpdateUser = "HUB";
        await UpdateCaseStatusAsync(entry, "Failed", null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task UpdateCaseStatusAsync(IcpOutboxEntry entry, string status, string? caseNo, CancellationToken cancellationToken)
    {
        var parts = (entry.HeaderKey ?? string.Empty).Split('\u001f', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            _logger.LogWarning("Cannot update ICP case status: invalid HeaderKey for outbox {MessageId}.", entry.Id);
            return;
        }

        var isArur = string.Equals(entry.CaseType, "ARUR", StringComparison.OrdinalIgnoreCase);
        var headerStatusColumn = isArur ? "ARUR_CASE_STATUS" : "DEPOSIT_CASE_STATUS";
        var detailStatusColumn = isArur ? "ARUR_CASE_STATUS" : "DEPOSIT_CASE_STATUS";
        var caseColumn = isArur ? "RT_NO" : "DEPOSIT";
        var caseValue = status == "Initiated" ? caseNo : null;
        var connection = _db.Database.GetDbConnection();
        var mustClose = connection.State != System.Data.ConnectionState.Open;
        if (mustClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"UPDATE dbo.ICP_HEADER SET {headerStatusColumn}=@status, {caseColumn}=@caseNo, UpdateTime=GETDATE(), UpdateUser=N'HUB' WHERE INVOICE_NO=@invoiceNo AND ISNULL(TET_PO,N'')=@tetPo; UPDATE dbo.ICP_DETAIL SET {detailStatusColumn}=@status, UpdateTime=GETDATE(), UpdateUser=N'HUB' WHERE INVOICE_NO=@invoiceNo AND ISNULL(TET_PO,N'')=@tetPo;";
            var statusParameter = command.CreateParameter(); statusParameter.ParameterName = "@status"; statusParameter.Value = status; command.Parameters.Add(statusParameter);
            var caseParameter = command.CreateParameter(); caseParameter.ParameterName = "@caseNo"; caseParameter.Value = (object?)caseValue ?? DBNull.Value; command.Parameters.Add(caseParameter);
            var invoiceParameter = command.CreateParameter(); invoiceParameter.ParameterName = "@invoiceNo"; invoiceParameter.Value = parts[0]; command.Parameters.Add(invoiceParameter);
            var poParameter = command.CreateParameter(); poParameter.ParameterName = "@tetPo"; poParameter.Value = parts[1]; command.Parameters.Add(poParameter);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (mustClose) await connection.CloseAsync();
        }
    }
}
