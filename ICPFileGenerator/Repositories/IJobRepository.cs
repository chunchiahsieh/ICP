using ICPFileGenerator.Models;

namespace ICPFileGenerator.Repositories;

public interface IJobRepository
{
    Task<int> ResetTimeoutJobsAsync(
        int processingTimeoutMinutes,
        int maxRetryCount,
        CancellationToken cancellationToken = default);

    Task<FileGenerationJob?> ClaimNextAsync(string workerId, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid jobId,
        string outputFilePath,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid jobId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileGenerationJob>> QueryAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<FileGenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FileGenerationJob?> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
}
