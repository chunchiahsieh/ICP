using ICP.Data;
using ICP.Models.Auth;
using ICP.Models.Ilc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class UserInfoResolver
{
    private readonly IlcDbContext _ilcDb;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppAuthOptions _authOptions;

    public UserInfoResolver(
        IlcDbContext ilcDb,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AppAuthOptions> authOptions)
    {
        _ilcDb = ilcDb;
        _httpContextAccessor = httpContextAccessor;
        _authOptions = authOptions.Value;
    }

    public bool IsProductionMode =>
        string.Equals(_authOptions.Mode, "PRD", StringComparison.OrdinalIgnoreCase);

    public LoggedInUser? ResolveDevUser()
    {
        var dev = _authOptions.DevUser;
        if (string.IsNullOrWhiteSpace(dev.TelId))
        {
            return null;
        }

        return new LoggedInUser
        {
            TelId = dev.TelId.Trim(),
            UserName = dev.UserName,
            DisplayName = dev.DisplayName,
            EmailAddress = dev.EmailAddress,
            DepId = dev.DepId,
            DepName = dev.DepName
        };
    }

    public string? GetWindowsAccountName()
    {
        var identityName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return null;
        }

        var separatorIndex = identityName.LastIndexOf('\\');
        return separatorIndex >= 0
            ? identityName[(separatorIndex + 1)..]
            : identityName;
    }

    public async Task<LoggedInUser?> ResolveFromWindowsIdentityAsync(CancellationToken cancellationToken = default)
    {
        var accountName = GetWindowsAccountName();
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return null;
        }

        return await ResolveFromTelIdAsync(accountName, cancellationToken);
    }

    public async Task<LoggedInUser?> ResolveFromTelIdAsync(
        string telId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(telId))
        {
            return null;
        }

        var normalizedTelId = telId.Trim();
        var entity = await _ilcDb.UserInfoAd
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelId == normalizedTelId, cancellationToken);

        return entity is null ? null : MapFromEntity(entity);
    }

    private static LoggedInUser MapFromEntity(UserInfoAd entity) =>
        new()
        {
            KeyId = entity.KeyId,
            TelId = entity.TelId?.Trim() ?? string.Empty,
            UserName = entity.UserName,
            DisplayName = entity.DisplayName,
            EmailAddress = entity.EmailAddress,
            DepId = entity.DepId,
            DepName = entity.DepName
        };
}
