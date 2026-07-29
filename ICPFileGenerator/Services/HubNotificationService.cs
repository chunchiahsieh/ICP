using System.Net.Http.Json;
using ICPFileGenerator.Infrastructure.Logging;
using ICPFileGenerator.Models;
using Microsoft.Extensions.Options;

namespace ICPFileGenerator.Services;

public sealed class HubNotificationService : IHubNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileGeneratorOptions _options;
    private readonly ILogger<HubNotificationService> _logger;

    public HubNotificationService(
        IHttpClientFactory httpClientFactory,
        IOptions<FileGeneratorOptions> options,
        ILogger<HubNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyCompletedAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("HubDemo");
        using var response = await client.PostAsJsonAsync(
            "api/demo/file-jobs/completed",
            new { requestId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation(
            "[{Category}] Notified Hub Completed RequestId={RequestId}",
            FileGeneratorLogCategories.Hub,
            requestId);
    }

    public async Task NotifyFailedAsync(Guid requestId, string error, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("HubDemo");
        using var response = await client.PostAsJsonAsync(
            "api/demo/file-jobs/failed",
            new { requestId, error },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogWarning(
            "[{Category}] Notified Hub Failed RequestId={RequestId} Error={Error}",
            FileGeneratorLogCategories.Hub,
            requestId,
            error);
    }
}
