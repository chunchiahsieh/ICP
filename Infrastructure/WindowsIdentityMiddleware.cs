using System.Security.Claims;
using System.Security.Principal;
using ICP.Models.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Options;

namespace ICP.Infrastructure;

/// <summary>
/// 每個 Request 設定 Windows 身分，等效舊版 IIS 的 Request.LogonUserIdentity。
/// AGA-PC 且設定 SimulatedWindowsIdentity 時一律覆寫；否則沿用 IIS 身分或 Negotiate 驗證。
/// </summary>
public class WindowsIdentityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppAuthOptions _authOptions;
    private readonly ILogger<WindowsIdentityMiddleware> _logger;

    public WindowsIdentityMiddleware(
        RequestDelegate next,
        IOptions<AppAuthOptions> authOptions,
        ILogger<WindowsIdentityMiddleware> logger)
    {
        _next = next;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HostEnvironmentExtensions.IsAgaComputer() &&
            !string.IsNullOrWhiteSpace(_authOptions.SimulatedWindowsIdentity))
        {
            var simulatedName = _authOptions.SimulatedWindowsIdentity.Trim();
            context.User = new ClaimsPrincipal(new GenericIdentity(simulatedName, "SimulatedWindows"));
            HttpRequestLogonUserIdentityExtensions.SetLogonUserIdentityName(context, simulatedName);
            _logger.LogInformation(
                "Simulated Windows identity {WindowsIdentityName}",
                simulatedName);
        }
        else
        {
            var windowsIdentityName = await ResolveWindowsIdentityName(context);
            if (!string.IsNullOrWhiteSpace(windowsIdentityName))
            {
                ApplyWindowsIdentity(context, windowsIdentityName);
                _logger.LogInformation(
                    "IIS Windows identity {WindowsIdentityName}",
                    windowsIdentityName);
            }
            else
            {
                _logger.LogWarning(
                    "Windows identity not available. Use IIS Express profile with Windows Authentication enabled (not Kestrel https).");
            }
        }

        await _next(context);
    }

    private async Task<string?> ResolveWindowsIdentityName(HttpContext context)
    {
        var existingName = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(existingName))
        {
            return existingName;
        }

        var iisUserName = IisWindowsIdentityHelper.TryGetAuthenticatedUserName(context);
        if (!string.IsNullOrWhiteSpace(iisUserName))
        {
            return iisUserName;
        }

        if (IisWindowsIdentityHelper.IsRunningOnIis(context))
        {
            var iisResult = await context.AuthenticateAsync(IISDefaults.AuthenticationScheme);
            if (iisResult.Succeeded && iisResult.Principal?.Identity?.Name is { Length: > 0 } iisPrincipalName)
            {
                context.User = iisResult.Principal;
                return iisPrincipalName;
            }
        }

        var negotiateResult = await context.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
        if (negotiateResult.Succeeded && negotiateResult.Principal?.Identity?.Name is { Length: > 0 } negotiateName)
        {
            context.User = negotiateResult.Principal;
            return negotiateName;
        }

        _logger.LogDebug(
            "Negotiate authentication did not succeed: {FailureMessage}",
            negotiateResult.Failure?.Message ?? "no principal");

        return null;
    }

    private static void ApplyWindowsIdentity(HttpContext context, string userName)
    {
        HttpRequestLogonUserIdentityExtensions.SetLogonUserIdentityName(context, userName);

        if (string.IsNullOrWhiteSpace(context.User.Identity?.Name))
        {
            context.User = new ClaimsPrincipal(
                new GenericIdentity(userName, NegotiateDefaults.AuthenticationScheme));
        }
    }
}
