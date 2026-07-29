namespace ICPFileGenerator.Services;

public interface IHubNotificationService
{
    Task NotifyCompletedAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task NotifyFailedAsync(Guid requestId, string error, CancellationToken cancellationToken = default);
}
