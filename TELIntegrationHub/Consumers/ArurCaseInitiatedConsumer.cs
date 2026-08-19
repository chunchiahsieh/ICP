using System.Text.Json;
using MassTransit;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Consumers;

/// <summary>Consumes icp.shipinfo.case.initiated when payload.caseType = ARUR.</summary>
public sealed class ArurCaseInitiatedConsumer : IConsumer<JsonDocument>
{
    private readonly IMessageLogService _messageLogService;
    private readonly IIcpOutboxCompletionService _outboxCompletion;
    private readonly ILogger<ArurCaseInitiatedConsumer> _logger;

    public ArurCaseInitiatedConsumer(
        IMessageLogService messageLogService,
        IIcpOutboxCompletionService outboxCompletion,
        ILogger<ArurCaseInitiatedConsumer> logger)
    {
        _messageLogService = messageLogService;
        _outboxCompletion = outboxCompletion;
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
            _logger.LogInformation(
                "Handled ARUR 起案 event {MessageId} caseNo={CaseNo} correlationId={CorrelationId}",
                messageId,
                message.Payload?.CaseNo,
                message.CorrelationId);
            await _messageLogService.MarkSuccessAsync(log.Id, context.CancellationToken);
            if (message.MessageId != Guid.Empty)
            {
                await _outboxCompletion.MarkCompletedAsync(message.MessageId, context.CancellationToken);
            }

            //ARUR起案logic


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling ARUR 起案 event {MessageId}", messageId);
            await _messageLogService.MarkFailedAsync(log.Id, ex.Message, context.CancellationToken);
            throw;
        }
    }
}
