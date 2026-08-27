using System.Text.Json;
using MassTransit;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Consumers;

/// <summary>Consumes icp.shipinfo.case.initiated when payload.caseType = ARUR; writes ILC RT_ARUR_HEADER.</summary>
public sealed class ArurCaseInitiatedConsumer : IConsumer<JsonDocument>
{
    private readonly IMessageLogService _messageLogService;
    private readonly IIcpOutboxCompletionService _outboxCompletion;
    private readonly IIlcArurWriteService _ilcArurWrite;
    private readonly ILogger<ArurCaseInitiatedConsumer> _logger;

    public ArurCaseInitiatedConsumer(
        IMessageLogService messageLogService,
        IIcpOutboxCompletionService outboxCompletion,
        IIlcArurWriteService ilcArurWrite,
        ILogger<ArurCaseInitiatedConsumer> logger)
    {
        _messageLogService = messageLogService;
        _outboxCompletion = outboxCompletion;
        _ilcArurWrite = ilcArurWrite;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonDocument> context)
    {
        var raw = context.Message.RootElement.GetRawText();
        if (!IntegrationEventEnvelopeNormalizer.TryNormalizeShipInfoCase(raw, out var message, out var normalizedJson)
            || message is null)
        {
            // Parse failures are recorded by DepositCaseInitiatedConsumer to avoid duplicate Failed logs.
            return;
        }

        var caseType = message.Payload?.CaseType;
        if (!string.Equals(caseType, IcpIntegrationBusinessTypes.Arur, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
            normalizedJson,
            IcpIntegrationBusinessTypes.Arur,
            context.CancellationToken);

        try
        {
            var writeResult = await _ilcArurWrite.WriteFromShipInfoCaseAsync(message, context.CancellationToken);
            _logger.LogInformation(
                "Handled ARUR case event {MessageId} caseNo={CaseNo} RT_NO={RtNo} skippedDuplicate={Skipped}",
                messageId,
                message.Payload?.CaseNo,
                writeResult.RtNo,
                writeResult.SkippedDuplicate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling ARUR case event {MessageId}", messageId);
            await _messageLogService.MarkFailedAsync(log.Id, ex.Message, context.CancellationToken);

            var markedFailed = message.MessageId != Guid.Empty
                && await _outboxCompletion.MarkArurFailedAsync(message.MessageId, ex.Message, context.CancellationToken);
            if (!markedFailed)
            {
                throw new InvalidOperationException(
                    $"ARUR ILC write failed and ICP Outbox could not be marked Failed for {messageId}.",
                    ex);
            }

            // Outbox is Failed: ACK so MassTransit does not auto-retry the business error.
            return;
        }

        try
        {
            await _messageLogService.MarkSuccessAsync(log.Id, context.CancellationToken);
            if (message.MessageId != Guid.Empty)
            {
                await _outboxCompletion.MarkCompletedAsync(message.MessageId, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ARUR ILC write succeeded but ack failed for {MessageId}; leave Outbox unchanged so it is not marked Failed.",
                messageId);
        }
    }
}
