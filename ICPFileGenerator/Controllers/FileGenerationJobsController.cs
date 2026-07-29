using ICPFileGenerator.Models;
using ICPFileGenerator.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ICPFileGenerator.Controllers;

[ApiController]
[Route("api/file-generation-jobs")]
public class FileGenerationJobsController : ControllerBase
{
    private readonly IJobRepository _jobs;

    public FileGenerationJobsController(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    /// <summary>List jobs; optional status filter (Pending/Processing/Completed/Failed).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FileGenerationJob>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FileGenerationJob>>> GetJobs(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var items = await _jobs.QueryAsync(status, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileGenerationJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileGenerationJob>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("request/{requestId:guid}")]
    [ProducesResponseType(typeof(FileGenerationJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileGenerationJob>> GetByRequestId(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByRequestIdAsync(requestId, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }
}
