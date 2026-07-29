using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageLogService _messageLogService;

    public MessagesController(IMessageLogService messageLogService)
        => _messageLogService = messageLogService;

    [HttpGet]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? sourceSystem,
        [FromQuery] string? targetSystem,
        [FromQuery] string? eventType,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        MessageLogStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<MessageLogStatus>(status, ignoreCase: true, out var s))
        {
            parsedStatus = s;
        }

        var items = await _messageLogService.QueryAsync(new MessageLogQuery
        {
            SourceSystem = sourceSystem,
            TargetSystem = targetSystem,
            EventType = eventType,
            Status = parsedStatus,
            From = from,
            To = to,
            Take = take
        }, cancellationToken);

        return Ok(items.Select(Map));
    }

    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors(CancellationToken cancellationToken)
    {
        var items = await _messageLogService.GetErrorsAsync(cancellationToken);
        return Ok(items.Select(Map));
    }

    [HttpGet("{messageId}")]
    public async Task<IActionResult> GetByMessageId(string messageId, CancellationToken cancellationToken)
    {
        var item = await _messageLogService.GetByMessageIdAsync(messageId, cancellationToken);
        return item is null ? NotFound() : Ok(Map(item));
    }

    [HttpPost("{messageId}/retry")]
    public IActionResult Retry(string messageId)
        => StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Retry is not implemented in Phase 1.",
            messageId
        });

    private static object Map(MessageLog x) => new
    {
        x.Id,
        x.MessageId,
        x.CorrelationId,
        x.EventType,
        x.SourceSystem,
        x.TargetSystem,
        x.Status,
        x.RetryCount,
        x.ErrorMessage,
        ReceivedAt = x.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        ProcessedAt = x.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        CreateTime = x.CreateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        UpdateTime = x.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        x.Payload
    };
}
