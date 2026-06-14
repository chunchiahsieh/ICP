using System.Text.Json;
using ICP.Data;
using ICP.Infrastructure;
using ICP.Models.Auth;
using ICP.Models.Ilc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class UserAuthService
{
    public const string SessionKey = "UserInfo";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IlcDbContext _ilcDb;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppAuthOptions _authOptions;
    private readonly UserResourcePermissionService _userResourcePermissionService;

    public UserAuthService(
        IlcDbContext ilcDb,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AppAuthOptions> authOptions,
        UserResourcePermissionService userResourcePermissionService)
    {
        _ilcDb = ilcDb;
        _httpContextAccessor = httpContextAccessor;
        _authOptions = authOptions.Value;
        _userResourcePermissionService = userResourcePermissionService;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public bool IsProductionMode =>
        string.Equals(_authOptions.Mode, "PRD", StringComparison.OrdinalIgnoreCase);

    public bool IsAuthenticated
    {
        get
        {
            var user = GetSessionUserInfo();
            return user is not null && !string.IsNullOrWhiteSpace(user.TelId);
        }
    }

    /// <summary>等效舊版 BaseController.GetUserInfo。</summary>
    public async Task<UserInfoAd> GetUserInfo(string TELID = "", CancellationToken cancellationToken = default)
    {
        var request = _httpContextAccessor.HttpContext!.Request;

        string LoginAccount = request.LogonUserIdentity().Name.Split('\\').Last();
        if (!string.IsNullOrEmpty(TELID))
        {
            LoginAccount = TELID;
        }

        var UserInfo = await _ilcDb.UserInfoAd
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelId == LoginAccount, cancellationToken);

        if (UserInfo is null)
        {
            UserInfo = new UserInfoAd();
        }

        return UserInfo;
    }

    /// <summary>SuperUser 關閉時，僅允許與主機 Windows 身分在 ILC 中對應的 TELID 登入。</summary>
    public async Task<bool> CanLoginAsync(string telId = "", CancellationToken cancellationToken = default)
    {
        var userInfo = await GetUserInfo(telId, cancellationToken);
        if (string.IsNullOrEmpty(userInfo.TelId))
        {
            return false;
        }

        if (_authOptions.IsSuperUserEnabled)
        {
            return true;
        }

        var hostUserInfo = await GetUserInfo(cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(hostUserInfo.TelId))
        {
            return false;
        }

        return string.Equals(userInfo.TelId, hostUserInfo.TelId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>等效舊版 BaseController.TempDataSet（僅 Windows 登入核心，不含 FuncGroup）。</summary>
    public async Task<bool> TempDataSet(
        Controller controller,
        string login = "",
        string type = "",
        CancellationToken cancellationToken = default)
    {
        _ = type;
        UserInfoAd userInfo;

        if (IsProductionMode)
        {
            if (string.IsNullOrEmpty(login))
            {
                userInfo = await GetUserInfo(cancellationToken: cancellationToken);
                if (string.IsNullOrEmpty(userInfo.TelId))
                {
                    return false;
                }
            }
            else
            {
                if (!await CanLoginAsync(login, cancellationToken))
                {
                    return false;
                }

                userInfo = await GetUserInfo(login, cancellationToken);
            }
        }
        else
        {
            userInfo = new UserInfoAd
            {
                DepName = "DEVTEST",
                DisplayName = "DEV",
                EmailAddress = "dev@example.com",
                TelId = "00000",
                UserName = "DEV",
                KeyId = 0
            };
        }

        SetSessionUserInfo(userInfo);
        await _userResourcePermissionService.RefreshSessionResourcesAsync(userInfo, cancellationToken);
        controller.TempData["DepName"] = userInfo.DepName;
        controller.TempData["DisplayName"] = userInfo.DisplayName;
        controller.TempData["EmailAddress"] = userInfo.EmailAddress;
        controller.TempData["TELID"] = userInfo.TelId;
        controller.TempData["UserName"] = userInfo.UserName;
        controller.TempData["DepID"] = userInfo.DepId;

        return true;
    }

    public UserInfoAd? GetSessionUserInfo()
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

        return JsonSerializer.Deserialize<UserInfoAd>(json, JsonOptions);
    }

    public void SetSessionUserInfo(UserInfoAd userInfo)
    {
        var session = Session;
        if (session is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(userInfo, JsonOptions);
        session.SetString(SessionKey, json);
    }

    public void SessionClear(Controller? controller = null)
    {
        Session?.Remove(SessionKey);
        _userResourcePermissionService.ClearSessionResources();

        if (controller is null)
        {
            return;
        }

        controller.TempData.Remove("DepName");
        controller.TempData.Remove("DisplayName");
        controller.TempData.Remove("EmailAddress");
        controller.TempData.Remove("TELID");
        controller.TempData.Remove("UserName");
        controller.TempData.Remove("DepID");
    }
}
