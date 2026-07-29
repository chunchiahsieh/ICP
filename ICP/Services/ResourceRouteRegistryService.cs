using ICP.Data;
using ICP.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

/// <summary>快取 DB Resources 的 Route 與 ResourceCode 索引，供後端 Route 權限中間層查詢。</summary>
public sealed class ResourceRouteRegistryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private ResourceRouteSnapshot _snapshot = ResourceRouteSnapshot.Empty;

    public ResourceRouteRegistryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resources = await db.Resources
            .AsNoTracking()
            .Where(r => r.IsEnabled && r.Route != null && r.Route != "")
            .Select(r => new { r.ResourceCode, r.ResourceType, r.Route })
            .ToListAsync(cancellationToken);

        var pageByRoute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var registeredCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            registeredCodes.Add(resource.ResourceCode);

            if (!resource.ResourceType.Equals("Page", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedRoute = PermissionRouteNormalizer.NormalizeRoute(resource.Route!);
            pageByRoute[normalizedRoute] = resource.ResourceCode;
        }

        lock (_lock)
        {
            _snapshot = new ResourceRouteSnapshot(pageByRoute, registeredCodes);
        }
    }

    public string? FindPageResourceCodeByRoute(string route)
    {
        var normalizedRoute = PermissionRouteNormalizer.NormalizeRoute(route);
        lock (_lock)
        {
            return _snapshot.PageByRoute.TryGetValue(normalizedRoute, out var code) ? code : null;
        }
    }

    public bool IsRegisteredResourceCode(string resourceCode)
    {
        if (string.IsNullOrWhiteSpace(resourceCode))
        {
            return false;
        }

        lock (_lock)
        {
            return _snapshot.RegisteredCodes.Contains(resourceCode.Trim());
        }
    }

    private sealed class ResourceRouteSnapshot
    {
        public static ResourceRouteSnapshot Empty { get; } =
            new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public ResourceRouteSnapshot(
            Dictionary<string, string> pageByRoute,
            HashSet<string> registeredCodes)
        {
            PageByRoute = pageByRoute;
            RegisteredCodes = registeredCodes;
        }

        public Dictionary<string, string> PageByRoute { get; }

        public HashSet<string> RegisteredCodes { get; }
    }
}
