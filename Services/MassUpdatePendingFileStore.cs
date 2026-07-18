using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace ICP.Services;

public class MassUpdatePendingFileStore
{
    private const string SessionCookieName = ".AspNetCore.Session";
    private const string CacheKeyPrefix = "massupdate:pending:";
    private static readonly TimeSpan PendingSlidingExpiration = TimeSpan.FromHours(8);

    private readonly IDistributedCache _cache;

    public MassUpdatePendingFileStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public void Add(HttpContext httpContext, string normalizedPath) =>
        _cache.SetString(
            BuildCacheKey(httpContext, normalizedPath),
            Path.GetFullPath(normalizedPath.Trim()),
            new DistributedCacheEntryOptions { SlidingExpiration = PendingSlidingExpiration });

    public bool Contains(HttpContext httpContext, string normalizedPath) =>
        !string.IsNullOrEmpty(_cache.GetString(BuildCacheKey(httpContext, normalizedPath)));

    public void Remove(HttpContext httpContext, string normalizedPath) =>
        _cache.Remove(BuildCacheKey(httpContext, normalizedPath));

    private static string BuildCacheKey(HttpContext httpContext, string normalizedPath)
    {
        var owner = httpContext.Request.Cookies[SessionCookieName]
            ?? httpContext.User.Identity?.Name
            ?? httpContext.TraceIdentifier;
        var path = Path.GetFullPath(normalizedPath.Trim()).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
        return $"{CacheKeyPrefix}{owner}:{hash}";
    }
}
