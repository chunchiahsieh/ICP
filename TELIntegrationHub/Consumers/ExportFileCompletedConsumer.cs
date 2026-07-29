using System.Text.Json;
using MassTransit;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Consumers;

/// <summary>Consumes reserved routing key icp.export.completed (Envelope).</summary>
public sealed class ExportFileCompletedConsumer : IConsumer<JsonDocument>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IMessageLogService _messageLogService;
    private readonly IIcpOutboxCompletionService _outboxCompletion;
    private readonly ILogger<ExportFileCompletedConsumer> _logger;

    public ExportFileCompletedConsumer(
        IMessageLogService messageLogService,
        IIcpOutboxCompletionService outboxCompletion,
        ILogger<ExportFileCompletedConsumer> logger)
    {
        _messageLogService = messageLogService;
        _outboxCompletion = outboxCompletion;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonDocument> context)
    {
        var raw = context.Message.RootElement.GetRawText();
        ExportFileCompletedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<ExportFileCompletedMessage>(raw, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Export Envelope.");
            message = null;
        }

        if (message is null || message.Payload is null)
        {
            var fallbackId = context.MessageId?.ToString("D") ?? Guid.NewGuid().ToString("D");
            var error = "Unable to parse Export event as standard Envelope.";
            var failedLog = await _messageLogService.RecordReceivedAsync(
                fallbackId,
                IcpIntegrationEventTypes.ExportCompleted,
                "ICP",
                null,
                raw,
                null,
                context.CancellationToken);
            await _messageLogService.MarkFailedAsync(failedLog.Id, error, context.CancellationToken);
            return;
        }

        var messageId = message.MessageId != Guid.Empty
            ? message.MessageId.ToString("D")
            : Guid.NewGuid().ToString("D");

        var log = await _messageLogService.RecordReceivedAsync(
            messageId,
            string.IsNullOrWhiteSpace(message.EventType)
                ? IcpIntegrationEventTypes.ExportCompleted
                : message.EventType,
            string.IsNullOrWhiteSpace(message.SourceSystem) ? "ICP" : message.SourceSystem,
            message.CorrelationId,
            raw,
            IcpIntegrationBusinessTypes.Export,
            context.CancellationToken);

        try
        {
            _logger.LogInformation(
                "Handled 匯出檔案 event {MessageId} fileName={FileName} exportType={ExportType}",
                messageId,
                message.Payload.FileName,
                message.Payload.ExportType);
            await _messageLogService.MarkSuccessAsync(log.Id, context.CancellationToken);
            if (message.MessageId != Guid.Empty)
            {
                await _outboxCompletion.MarkCompletedAsync(message.MessageId, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling 匯出檔案 event {MessageId}", messageId);
            await _messageLogService.MarkFailedAsync(log.Id, ex.Message, context.CancellationToken);
            throw;
        }
    }
}
