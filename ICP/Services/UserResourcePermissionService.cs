using System.Text.Json;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Auth;
using ICP.Models.Icp;
using ICP.Models.Ilc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class UserResourcePermissionService
{
    public const string SessionKey = "UserResources";

    private static readonly JsonSerializerOptions SessionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly FiestaDbContext _fiestaDb;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppAuthOptions _authOptions;

    public UserResourcePermissionService(
        ApplicationDbContext icpDb,
        FiestaDbContext fiestaDb,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AppAuthOptions> authOptions)
    {
        _icpDb = icpDb;
        _fiestaDb = fiestaDb;
        _httpContextAccessor = httpContextAccessor;
        _authOptions = authOptions.Value;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public bool IsSuperUserEnabled => _authOptions.IsSuperUserEnabled;

    public bool HasAllow(string resourceCode) => HasPermission(resourceCode);

    public bool HasMenuCategoryPermission(string resourceCode)
    {
        if (string.IsNullOrWhiteSpace(resourceCode))
        {
            return false;
        }

        if (_authOptions.IsSuperUserEnabled)
        {
            return true;
        }

        var trimmedCode = resourceCode.Trim();
        return GetSessionResources().Any(r =>
            r.IsAllowed
            && r.ResourceCode.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            && r.ResourceType.Equals("Menu Category", StringComparison.OrdinalIgnoreCase)
            && r.ActionCode.Equals(RolePermissionActionCodes.Allow, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasMenuPermission(string resourceCode)
    {
        if (string.IsNullOrWhiteSpace(resourceCode))
        {
            return false;
        }

        if (_authOptions.IsSuperUserEnabled)
        {
            return true;
        }

        var trimmedCode = resourceCode.Trim();
        return GetSessionResources().Any(r =>
            r.IsAllowed
            && r.ResourceCode.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            && r.ResourceType.Equals("Menu", StringComparison.OrdinalIgnoreCase)
            && r.ActionCode.Equals(RolePermissionActionCodes.Allow, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasPermission(string resourceCode)
    {
        if (_authOptions.IsSuperUserEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(resourceCode))
        {
            return false;
        }

        var trimmedCode = resourceCode.Trim();
        return GetSessionResources().Any(r =>
            r.IsAllowed
            && r.ResourceCode.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            && MatchesGrant(r));
    }

    private static bool MatchesGrant(UserResourceItem resource)
    {
        if (string.IsNullOrWhiteSpace(resource.ResourceType))
        {
            return false;
        }

        if (resource.ResourceType.Equals("Menu Category", StringComparison.OrdinalIgnoreCase)
            || resource.ResourceType.Equals("Menu", StringComparison.OrdinalIgnoreCase))
        {
            return resource.ActionCode.Equals(RolePermissionActionCodes.Allow, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    public IReadOnlySet<string> GetAllowedResourceCodes()
    {
        if (_authOptions.IsSuperUserEnabled)
        {
            return EmptyResourceCodes;
        }

        return GetSessionResources()
            .Where(r => r.IsAllowed && !string.IsNullOrWhiteSpace(r.ResourceCode) && MatchesGrant(r))
            .Select(r => r.ResourceCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> EmptyResourceCodes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<UserResourceItem> GetSessionResources()
    {
        var session = Session;
        if (session is null)
        {
            return [];
        }

        var json = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<UserResourceItem>>(json, SessionJsonOptions) ?? [];
    }

    public void ClearSessionResources()
    {
        Session?.Remove(SessionKey);
    }

    public async Task EnsureSessionResourcesAsync(UserInfoAd user, CancellationToken cancellationToken = default)
    {
        if (_authOptions.IsSuperUserEnabled)
        {
            return;
        }

        var session = Session;
        if (session is null || !string.IsNullOrWhiteSpace(session.GetString(SessionKey)))
        {
            return;
        }

        await RefreshSessionResourcesAsync(user, cancellationToken);
    }

    public async Task RefreshSessionResourcesAsync(UserInfoAd user, CancellationToken cancellationToken = default)
    {
        if (_authOptions.IsSuperUserEnabled)
        {
            return;
        }

        var resources = await BuildUserResourcesAsync(user, cancellationToken);
        var session = Session;
        if (session is null)
        {
            return;
        }

        session.SetString(SessionKey, JsonSerializer.Serialize(resources, SessionJsonOptions));
    }

    public async Task<UserPermissionsResponse> BuildPermissionsResponseAsync(
        UserInfoAd user,
        CancellationToken cancellationToken = default)
    {
        var (assignments, orderedRoleIds) = await BuildUserRoleAssignmentsAsync(user, cancellationToken);

        return new UserPermissionsResponse
        {
            KeyId = user.KeyId,
            DepName = user.DepName,
            UserName = user.UserName,
            TelId = user.TelId,
            EmailAddress = user.EmailAddress,
            DisplayName = user.DisplayName,
            DepId = user.DepId,
            DepName2 = user.DepName2,
            CreateDate = user.CreateDate,
            RoleAssignments = assignments,
            Resources = await BuildUserResourcesFromRoleIdsAsync(orderedRoleIds, cancellationToken)
        };
    }

    public async Task<List<UserResourceItem>> BuildUserResourcesAsync(
        UserInfoAd user,
        CancellationToken cancellationToken = default)
    {
        var (_, orderedRoleIds) = await BuildUserRoleAssignmentsAsync(user, cancellationToken);
        return await BuildUserResourcesFromRoleIdsAsync(orderedRoleIds, cancellationToken);
    }

    private async Task<(UserRoleAssignmentsDetail Assignments, List<Guid> OrderedRoleIds)> BuildUserRoleAssignmentsAsync(
        UserInfoAd user,
        CancellationToken cancellationToken)
    {
        var telId = user.TelId?.Trim();
        var depId = user.DepId?.Trim();
        var orderedRoleIds = new List<Guid>();

        var telEntities = string.IsNullOrEmpty(telId)
            ? []
            : await _icpDb.RolesTelId
                .AsNoTracking()
                .Include(r => r.Role)
                .Where(r => r.IsEnabled && r.Role.IsEnabled && r.TelId == telId)
                .OrderBy(r => r.Role.RoleCode)
                .ToListAsync(cancellationToken);

        var roleTelIds = telEntities.Select(r => new UserRoleTelIdPermissionItem
        {
            Id = r.Id,
            TelId = r.TelId,
            RoleCode = r.Role.RoleCode,
            RoleName = r.Role.RoleName,
            IsEnabled = r.IsEnabled,
            Description = r.Description,
            CreateTime = r.CreateTime,
            CreateUser = r.CreateUser
        }).ToList();

        var depEntities = string.IsNullOrEmpty(depId)
            ? []
            : await _icpDb.RolesDepId
                .AsNoTracking()
                .Include(r => r.Role)
                .Where(r => r.IsEnabled && r.Role.IsEnabled && r.DepId == depId)
                .OrderBy(r => r.Role.RoleCode)
                .ToListAsync(cancellationToken);

        var roleDepIds = depEntities.Select(r => new UserRoleDepIdPermissionItem
        {
            Id = r.Id,
            DepId = r.DepId,
            RoleCode = r.Role.RoleCode,
            RoleName = r.Role.RoleName,
            IsEnabled = r.IsEnabled,
            Description = r.Description,
            CreateTime = r.CreateTime,
            CreateUser = r.CreateUser
        }).ToList();

        var roleMailGroups = new List<UserRoleMailGroupPermissionItem>();
        var mailGroupEntities = new List<RoleMailGroup>();
        if (!string.IsNullOrEmpty(telId))
        {
            var addresses = await _fiestaDb.MailGroup
                .AsNoTracking()
                .Where(m => m.EmpId == telId && m.Address != null && m.Address != "")
                .Select(m => m.Address!)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (addresses.Count > 0)
            {
                var mailGroups = await _fiestaDb.MailGroup
                    .AsNoTracking()
                    .Where(m => m.Address != null && addresses.Contains(m.Address))
                    .ToListAsync(cancellationToken);

                var mailGroupNameByAddress = mailGroups
                    .Where(m => !string.IsNullOrWhiteSpace(m.Address))
                    .GroupBy(m => m.Address!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

                mailGroupEntities = await _icpDb.RolesMailGroup
                    .AsNoTracking()
                    .Include(r => r.Role)
                    .Where(r => r.IsEnabled && r.Role.IsEnabled && addresses.Contains(r.Address))
                    .OrderBy(r => r.Address)
                    .ThenBy(r => r.Role.RoleCode)
                    .ToListAsync(cancellationToken);

                roleMailGroups = mailGroupEntities.Select(r =>
                {
                    mailGroupNameByAddress.TryGetValue(r.Address, out var mailGroupName);
                    return new UserRoleMailGroupPermissionItem
                    {
                        Id = r.Id,
                        Address = r.Address,
                        MailGroupName = mailGroupName,
                        RoleCode = r.Role.RoleCode,
                        RoleName = r.Role.RoleName,
                        IsEnabled = r.IsEnabled,
                        Description = r.Description,
                        CreateTime = r.CreateTime,
                        CreateUser = r.CreateUser
                    };
                }).ToList();
            }
        }

        orderedRoleIds.AddRange(mailGroupEntities.Select(r => r.RoleId));
        orderedRoleIds.AddRange(depEntities.Select(r => r.RoleId));
        orderedRoleIds.AddRange(telEntities.Select(r => r.RoleId));

        return (new UserRoleAssignmentsDetail
        {
            RoleTelIds = roleTelIds,
            RoleDepIds = roleDepIds,
            RoleMailGroups = roleMailGroups
        }, orderedRoleIds);
    }

    private async Task<List<UserResourceItem>> BuildUserResourcesFromRoleIdsAsync(
        List<Guid> orderedRoleIds,
        CancellationToken cancellationToken)
    {
        if (orderedRoleIds.Count == 0)
        {
            return [];
        }

        var uniqueRoleIds = orderedRoleIds.Distinct().ToList();
        var permissionsByRole = await _icpDb.RolePermissions
            .AsNoTracking()
            .Include(rp => rp.Resource)
            .Where(rp => uniqueRoleIds.Contains(rp.RoleId)
                && rp.IsAllowed
                && rp.Resource.IsEnabled)
            .GroupBy(rp => rp.RoleId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var resourceMap = new Dictionary<Guid, UserResourceItem>();

        foreach (var roleId in orderedRoleIds)
        {
            if (!permissionsByRole.TryGetValue(roleId, out var rolePermissions))
            {
                continue;
            }

            foreach (var rolePermission in rolePermissions)
            {
                var resource = rolePermission.Resource;
                resourceMap[resource.Id] = new UserResourceItem
                {
                    ResourceId = resource.Id,
                    ResourceCode = resource.ResourceCode,
                    ResourceName = resource.ResourceName,
                    ResourceType = resource.ResourceType,
                    SystemCode = resource.SystemCode,
                    ModuleCode = resource.ModuleCode,
                    Route = resource.Route,
                    ActionCode = rolePermission.ActionCode,
                    IsAllowed = rolePermission.IsAllowed
                };
            }
        }

        return resourceMap.Values
            .OrderBy(r => r.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
