using ICPFileGenerator.Infrastructure.Logging;
using ICPFileGenerator.Models;
using ICPFileGenerator.Repositories;
using ICPFileGenerator.Services;
using Microsoft.Extensions.Options;

namespace ICPFileGenerator.Workers;

public sealed class FileGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FileGeneratorOptions _options;
    private readonly ILogger<FileGenerationWorker> _logger;

    public FileGenerationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FileGeneratorOptions> options,
        ILogger<FileGenerationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, _options.PollingIntervalSeconds);
        _logger.LogInformation(
            "[{Category}] Started WorkerId={WorkerId} PollingIntervalSeconds={Interval}",
            FileGeneratorLogCategories.Worker,
            _options.WorkerId,
            intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Category}] Worker loop error", FileGeneratorLogCategories.Worker);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var generator = scope.ServiceProvider.GetRequiredService<IFileGenerationService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubNotificationService>();

        var resetCount = await jobs.ResetTimeoutJobsAsync(
            _options.ProcessingTimeoutMinutes,
            _options.MaxRetryCount,
            cancellationToken);
        if (resetCount > 0)
        {
            _logger.LogWarning(
                "[{Category}] Reset {Count} timed-out Processing job(s)",
                FileGeneratorLogCategories.Worker,
                resetCount);
        }

        var job = await jobs.ClaimNextAsync(_options.WorkerId, cancellationToken);
        if (job is null)
        {
            return;
        }

        _logger.LogInformation(
            "[{Category}] Claimed JobId={JobId} RequestId={RequestId}",
            FileGeneratorLogCategories.Worker,
            job.Id,
            job.RequestId);

        try
        {
            var result = await generator.GenerateAsync(job, cancellationToken);
            if (!result.Success || string.IsNullOrWhiteSpace(result.OutputFilePath))
            {
                var error = result.ErrorMessage ?? "File generation failed.";
                await jobs.MarkFailedAsync(job.Id, error, cancellationToken);
                await hub.NotifyFailedAsync(job.RequestId, error, cancellationToken);
                return;
            }

            await jobs.MarkCompletedAsync(job.Id, result.OutputFilePath, cancellationToken);
            await hub.NotifyCompletedAsync(job.RequestId, result.OutputFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Category}] Job failed JobId={JobId}",
                FileGeneratorLogCategories.Worker,
                job.Id);
            await jobs.MarkFailedAsync(job.Id, ex.Message, cancellationToken);
            await hub.NotifyFailedAsync(job.RequestId, ex.Message, cancellationToken);
        }
    }
}
