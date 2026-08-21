using System.Text.Json;
using MassTransit;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Consumers;

/// <summary>Consumes icp.shipinfo.case.initiated when payload.caseType = Deposit; writes ILC Deposit tables.</summary>
public sealed class DepositCaseInitiatedConsumer : IConsumer<JsonDocument>
{
    private readonly IMessageLogService _messageLogService;
    private readonly IIcpOutboxCompletionService _outboxCompletion;
    private readonly IIlcDepositWriteService _ilcDepositWrite;
    private readonly ILogger<DepositCaseInitiatedConsumer> _logger;

    public DepositCaseInitiatedConsumer(
        IMessageLogService messageLogService,
        IIcpOutboxCompletionService outboxCompletion,
        IIlcDepositWriteService ilcDepositWrite,
        ILogger<DepositCaseInitiatedConsumer> logger)
    {
        _messageLogService = messageLogService;
        _outboxCompletion = outboxCompletion;
        _ilcDepositWrite = ilcDepositWrite;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonDocument> context)
    {
        var raw = context.Message.RootElement.GetRawText();
        if (!IntegrationEventEnvelopeNormalizer.TryNormalizeShipInfoCase(raw, out var message, out var normalizedJson)
            || message is null)
        {
            var fallbackId = context.MessageId?.ToString("D") ?? Guid.NewGuid().ToString("D");
            var error = "Unable to parse ShipInfo case event as Envelope or legacy flat schema.";
            _logger.LogWarning("{Error} MessageId={MessageId}", error, fallbackId);

            var failedLog = await _messageLogService.RecordReceivedAsync(
                fallbackId,
                IcpIntegrationEventTypes.ShipInfoCaseInitiated,
                "ICP",
                correlationId: null,
                raw,
                targetSystem: null,
                context.CancellationToken);
            await _messageLogService.MarkFailedAsync(failedLog.Id, error, context.CancellationToken);
            return;
        }

        var caseType = message.Payload?.CaseType;
        if (string.Equals(caseType, IcpIntegrationBusinessTypes.Arur, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(caseType, IcpIntegrationBusinessTypes.Deposit, StringComparison.OrdinalIgnoreCase))
        {
            var messageId = message.MessageId != Guid.Empty
                ? message.MessageId.ToString("D")
                : Guid.NewGuid().ToString("D");
            var error = $"Unknown caseType '{caseType}'. Expected Deposit or ARUR.";
            _logger.LogWarning("No Deposit handler for ShipInfo case event {MessageId}: {Error}", messageId, error);

            var log = await _messageLogService.RecordReceivedAsync(
                messageId,
                string.IsNullOrWhiteSpace(message.EventType)
                    ? IcpIntegrationEventTypes.ShipInfoCaseInitiated
                    : message.EventType,
                string.IsNullOrWhiteSpace(message.SourceSystem) ? "ICP" : message.SourceSystem,
                message.CorrelationId,
                normalizedJson,
                targetSystem: null,
                context.CancellationToken);
            await _messageLogService.MarkFailedAsync(log.Id, error, context.CancellationToken);
            return;
        }

        await PersistSuccessAsync(message, normalizedJson, context.CancellationToken);
    }

    private async Task PersistSuccessAsync(
        ShipInfoCaseInitiatedMessage message,
        string envelopeJson,
        CancellationToken cancellationToken)
    {
        var messageId = message.MessageId != Guid.Empty
            ? message.MessageId.ToString("D")
            : Guid.NewGuid().ToString("D");

        var log = await _messageLogService.RecordReceivedAsync(
            messageId,
            string.IsNullOrWhiteSpace(message.EventType)
                ? IcpIntegrationEventTypes.ShipInfoCaseInitiated
                : message.EventType,
            string.IsNullOrWhiteSpace(message.SourceSystem) ? "ICP" : message.SourceSystem,
            message.CorrelationId,
            envelopeJson,
            IcpIntegrationBusinessTypes.Deposit,
            cancellationToken);

        try
        {
            var writeResult = await _ilcDepositWrite.WriteFromShipInfoCaseAsync(message, cancellationToken);
            _logger.LogInformation(
                "Handled Deposit case event {MessageId} caseNo={CaseNo} InvNo={CorrelationId} HeadKeyId={HeadKeyId} skippedDuplicate={Skipped}",
                messageId,
                message.Payload?.CaseNo,
                message.CorrelationId,
                writeResult.HeadKeyId,
                writeResult.SkippedDuplicate);

            await _messageLogService.MarkSuccessAsync(log.Id, cancellationToken);
            if (message.MessageId != Guid.Empty)
            {
                await _outboxCompletion.MarkCompletedAsync(message.MessageId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling Deposit case event {MessageId}", messageId);
            await _messageLogService.MarkFailedAsync(log.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
