using ICP.Models.Integration;
using ICP.Repositories;
using Microsoft.Extensions.Options;

namespace ICP.Services.Integration;

public class IntegrationEventOutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<IntegrationOptions> _options;
    private readonly ILogger<IntegrationEventOutboxPublisherWorker> _logger;

    public IntegrationEventOutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<IntegrationOptions> options,
        ILogger<IntegrationEventOutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var integrationOptions = _options.CurrentValue;
            var pollSeconds = Math.Max(1, integrationOptions.Outbox.PollIntervalSeconds);

            try
            {
                if (!integrationOptions.RabbitMq.Enabled)
                {
                    _logger.LogDebug("RabbitMQ publishing is disabled; outbox events remain pending.");
                }
                else
                {
                    await PublishPendingBatchAsync(integrationOptions, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Integration outbox publisher iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
        }
    }

    private async Task PublishPendingBatchAsync(IntegrationOptions integrationOptions, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutboxRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        var batch = await outboxRepository.GetPendingBatchAsync(
            integrationOptions.Outbox.BatchSize,
            integrationOptions.Outbox.MaxRetryCount,
            cancellationToken);

        foreach (var entry in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await publisher.PublishAsync(
                    integrationOptions.RabbitMq.RoutingKey,
                    entry.Id.ToString(),
                    entry.PayloadJson,
                    cancellationToken);

                await outboxRepository.MarkPublishedAsync(entry.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var nextRetryCount = entry.RetryCount + 1;
                var permanent = nextRetryCount >= integrationOptions.Outbox.MaxRetryCount;
                _logger.LogWarning(
                    ex,
                    "Failed to publish integration event {EventId}. RetryCount={RetryCount}, Permanent={Permanent}",
                    entry.Id,
                    nextRetryCount,
                    permanent);

                await outboxRepository.MarkFailedAsync(
                    entry.Id,
                    nextRetryCount,
                    ex.Message,
                    permanent,
                    cancellationToken);
            }
        }
    }
}
