using Microsoft.AspNetCore.Mvc;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoExportController : ControllerBase
{
    private readonly IExportDemoOrchestrationService _orchestration;

    public DemoExportController(IExportDemoOrchestrationService orchestration)
    {
        _orchestration = orchestration;
    }

    [HttpPost("export-requests")]
    public async Task<IActionResult> AcceptExportRequest(
        [FromBody] DemoExportRequestDto request,
        CancellationToken cancellationToken)
    {
        await _orchestration.AcceptExportRequestAsync(request, cancellationToken);
        return Ok(new { status = "accepted", requestId = request.RequestId });
    }

    [HttpPost("file-jobs/completed")]
    public async Task<IActionResult> FileJobCompleted(
        [FromBody] DemoFileJobCompletedDto body,
        CancellationToken cancellationToken)
    {
        await _orchestration.MarkExportCompletedAsync(body.RequestId, cancellationToken);
        return Ok(new { status = "completed", body.RequestId });
    }

    [HttpPost("file-jobs/failed")]
    public async Task<IActionResult> FileJobFailed(
        [FromBody] DemoFileJobFailedDto body,
        CancellationToken cancellationToken)
    {
        await _orchestration.MarkExportFailedAsync(body.RequestId, body.Error ?? "Unknown error", cancellationToken);
        return Ok(new { status = "failed", body.RequestId });
    }
}
