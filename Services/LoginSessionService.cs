using System.Text.Json;
using ICP.Models.Auth;
using Microsoft.AspNetCore.Http;

namespace ICP.Services;

public class LoginSessionService
{
    public const string SessionKey = "LoggedInUser";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserInfoResolver _userInfoResolver;

    public LoginSessionService(
        IHttpContextAccessor httpContextAccessor,
        UserInfoResolver userInfoResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _userInfoResolver = userInfoResolver;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public bool IsAuthenticated
    {
        get
        {
            var user = GetCurrentUser();
            return user is not null && !string.IsNullOrWhiteSpace(user.TelId);
        }
    }

    public LoggedInUser? GetCurrentUser()
    {
        var session = Session;
        if (session is null)
        {
            return null;
        }

        var json = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<LoggedInUser>(json, JsonOptions);
    }

    public async Task<bool> TryEstablishSessionAsync(
        string? loginTelId = null,
        CancellationToken cancellationToken = default)
    {
        LoggedInUser? user;

        if (!_userInfoResolver.IsProductionMode)
        {
            user = _userInfoResolver.ResolveDevUser();
        }
        else if (!string.IsNullOrWhiteSpace(loginTelId))
        {
            user = await _userInfoResolver.ResolveFromTelIdAsync(loginTelId, cancellationToken);
        }
        else
        {
            user = await _userInfoResolver.ResolveFromWindowsIdentityAsync(cancellationToken);
        }

        if (user is null || string.IsNullOrWhiteSpace(user.TelId))
        {
            return false;
        }

        SetCurrentUser(user);
        return true;
    }

    public void SetCurrentUser(LoggedInUser user)
    {
        var session = Session;
        if (session is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(user, JsonOptions);
        session.SetString(SessionKey, json);
    }

    public void ClearSession()
    {
        Session?.Remove(SessionKey);
    }
}
