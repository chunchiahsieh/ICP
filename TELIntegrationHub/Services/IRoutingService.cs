using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Services;

public interface IRoutingService
{
    Task<IReadOnlyList<RoutingRule>> GetRulesAsync(
        string sourceSystem,
        string eventType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoutingRule>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class StubRoutingService : IRoutingService
{
    public Task<IReadOnlyList<RoutingRule>> GetRulesAsync(
        string sourceSystem,
        string eventType,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RoutingRule>>(Array.Empty<RoutingRule>());

    public Task<IReadOnlyList<RoutingRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RoutingRule>>(Array.Empty<RoutingRule>());
}
