using ICPFileGenerator.Models;
using Microsoft.Extensions.Options;

namespace ICPFileGenerator.Services;

/// <summary>Demo: skip real file generation; mark job success with OutputFilePath = SKIPPED.</summary>
public sealed class FileGenerationService : IFileGenerationService
{
    private readonly ILogger<FileGenerationService> _logger;

    public FileGenerationService(ILogger<FileGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<FileGenerationResult> GenerateAsync(
        FileGenerationJob job,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Demo skip file generation for RequestId={RequestId} JobId={JobId}",
            job.RequestId,
            job.Id);
        return Task.FromResult(FileGenerationResult.Ok("SKIPPED"));
    }
}
