namespace ICP.Infrastructure;

/// <summary>
/// 從 IIS / IIS Express 讀取 Windows 登入帳號，等效舊版 Request.LogonUserIdentity.Name。
/// </summary>
internal static class IisWindowsIdentityHelper
{
    public static string? TryGetAuthenticatedUserName(HttpContext context)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        // AUTH_USER 通常為 DOMAIN\user，與舊版 LogonUserIdentity.Name 相同格式
        var authUser = context.GetServerVariable("AUTH_USER");
        if (!string.IsNullOrWhiteSpace(authUser))
        {
            return authUser.Trim();
        }

        var remoteUser = context.GetServerVariable("REMOTE_USER");
        if (!string.IsNullOrWhiteSpace(remoteUser))
        {
            return remoteUser.Trim();
        }

        var logonUser = context.GetServerVariable("LOGON_USER");
        return string.IsNullOrWhiteSpace(logonUser) ? null : logonUser.Trim();
    }

    public static bool IsRunningOnIis(HttpContext context)
    {
        var serverSoftware = context.GetServerVariable("SERVER_SOFTWARE");
        return !string.IsNullOrEmpty(serverSoftware) &&
               serverSoftware.Contains("Microsoft-IIS", StringComparison.OrdinalIgnoreCase);
    }
}
