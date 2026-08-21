namespace ICPFileGenerator.Services;

public interface IHubNotificationService
{
    Task NotifyCompletedAsync(
        Guid requestId,
        string? outputFilePath,
        CancellationToken cancellationToken = default);

    Task NotifyFailedAsync(Guid requestId, string error, CancellationToken cancellationToken = default);
}
