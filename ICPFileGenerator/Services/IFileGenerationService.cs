using ICPFileGenerator.Models;

namespace ICPFileGenerator.Services;

public interface IFileGenerationService
{
    Task<FileGenerationResult> GenerateAsync(FileGenerationJob job, CancellationToken cancellationToken = default);
}
