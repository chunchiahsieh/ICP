using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace ICP.Services;

public class AddDiSaPendingFileStore
{
    private const string SessionCookieName = ".AspNetCore.Session";
    private const string CacheKeyPrefix = "adddisa:pending:";
    private static readonly TimeSpan PendingSlidingExpiration = TimeSpan.FromHours(8);

    private readonly IDistributedCache _cache;

    public AddDiSaPendingFileStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public void Add(HttpContext httpContext, string normalizedPath)
    {
        var path = Path.GetFullPath(normalizedPath.Trim());
        var cacheKey = BuildCacheKey(httpContext, path);
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = PendingSlidingExpiration
        };

        _cache.SetString(cacheKey, path, options);
    }

    public bool Contains(HttpContext httpContext, string normalizedPath)
    {
        var path = Path.GetFullPath(normalizedPath.Trim());
        var cacheKey = BuildCacheKey(httpContext, path);
        return !string.IsNullOrEmpty(_cache.GetString(cacheKey));
    }

    public void Remove(HttpContext httpContext, string normalizedPath)
    {
        var path = Path.GetFullPath(normalizedPath.Trim());
        var cacheKey = BuildCacheKey(httpContext, path);
        _cache.Remove(cacheKey);
    }

    private static string BuildCacheKey(HttpContext httpContext, string normalizedPath)
    {
        var ownerKey = ResolveOwnerKey(httpContext);
        var pathKey = ComputePathKey(normalizedPath);
        return $"{CacheKeyPrefix}{ownerKey}:{pathKey}";
    }

    private static string ResolveOwnerKey(HttpContext httpContext)
    {
        var sessionCookie = httpContext.Request.Cookies[SessionCookieName];
        if (!string.IsNullOrWhiteSpace(sessionCookie))
        {
            return sessionCookie;
        }

        var userName = httpContext.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return httpContext.TraceIdentifier;
    }

    private static string ComputePathKey(string normalizedPath)
    {
        var bytes = Encoding.UTF8.GetBytes(normalizedPath.ToLowerInvariant());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
