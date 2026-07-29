namespace ICP.Infrastructure;

using ICP.Models.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>等效舊版 ASP.NET Request.LogonUserIdentity。</summary>
public sealed class LogonUserIdentity
{
    public string Name { get; }

    public LogonUserIdentity(string name) => Name = name;
}

public static class LogonUserIdentityContext
{
    public const string ItemsKey = "LogonUserIdentityName";
}

public static class HttpRequestLogonUserIdentityExtensions
{
    public static LogonUserIdentity LogonUserIdentity(this HttpRequest request)
    {
        var context = request.HttpContext;
        var name = ResolveLogonUserIdentityName(context);
        return new LogonUserIdentity(name);
    }

    internal static string ResolveLogonUserIdentityName(HttpContext context)
    {
        if (context.Items.TryGetValue(LogonUserIdentityContext.ItemsKey, out var cached) &&
            cached is string cachedName &&
            !string.IsNullOrWhiteSpace(cachedName))
        {
            return cachedName;
        }

        var identityName = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            return identityName;
        }

        if (HostEnvironmentExtensions.IsAgaComputer())
        {
            var authOptions = context.RequestServices
                .GetService<IOptions<AppAuthOptions>>()?
                .Value;

            if (!string.IsNullOrWhiteSpace(authOptions?.SimulatedWindowsIdentity))
            {
                return authOptions.SimulatedWindowsIdentity.Trim();
            }
        }

        if (HostEnvironmentExtensions.IsAgaComputer())
        {
            var logger = context.RequestServices
                .GetService<ILoggerFactory>()?
                .CreateLogger("LogonUserIdentity");

            logger?.LogWarning(
                "LogonUserIdentityName is empty on AGA-PC. Check WindowsIdentityMiddleware and App:SimulatedWindowsIdentity.");
        }

        return string.Empty;
    }

    internal static void SetLogonUserIdentityName(HttpContext context, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        context.Items[LogonUserIdentityContext.ItemsKey] = name;
    }
}
