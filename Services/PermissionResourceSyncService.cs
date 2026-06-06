using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class PermissionResourceSyncService
{
    private const string LegacyPrefix = "icp.";
    private const string PermissionPrefix = "icp.permission.";
    private const string ViewsPermissionPrefix = "Views.Permission.";

    private static readonly Dictionary<string, string> IcpPermissionToViewsPermission =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["icp.permission.user.view"] = "Views.Permission.Users.View",
            ["icp.permission.resource.view"] = "Views.Permission.RoleResources.View",
            ["icp.permission.resource.scan"] = "Views.Permission.RoleResources.Scan",
            ["icp.permission.role.view"] = "Views.Permission.Roles.View",
            ["icp.permission.role.create"] = "Views.Permission.Roles.Create",
            ["icp.permission.role.disable"] = "Views.Permission.Roles.Disable",
            ["icp.permission.role.edit"] = "Views.Permission.Roles.Edit",
            ["icp.permission.rolepermission.view"] = "Views.Permission.RolePermissions.View",
            ["icp.permission.rolepermission.create"] = "Views.Permission.RolePermissions.Create",
            ["icp.permission.rolepermission.delete"] = "Views.Permission.RolePermissions.Delete",
            ["icp.permission.roletelid.view"] = "Views.Permission.RoleTelIds.View",
            ["icp.permission.roletelid.create"] = "Views.Permission.RoleTelIds.Create",
            ["icp.permission.roletelid.delete"] = "Views.Permission.RoleTelIds.Delete",
            ["icp.permission.roledepid.view"] = "Views.Permission.RoleDepIds.View",
            ["icp.permission.roledepid.create"] = "Views.Permission.RoleDepIds.Create",
            ["icp.permission.roledepid.delete"] = "Views.Permission.RoleDepIds.Delete",
        };

    private static readonly Dictionary<string, string> ViewsPermissionToIcpPermission =
        IcpPermissionToViewsPermission.ToDictionary(
            kvp => kvp.Value,
            kvp => kvp.Key,
            StringComparer.OrdinalIgnoreCase);

    private readonly ApplicationDbContext _dbContext;

    public PermissionResourceSyncService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PermissionScanResult> UpsertAsync(
        IReadOnlyList<ScannedPermission> scannedItems,
        string? updateUser,
        CancellationToken cancellationToken = default)
    {
        var actor = string.IsNullOrWhiteSpace(updateUser) ? "PermissionScanner" : updateUser;
        var distinctItems = scannedItems
            .GroupBy(x => x.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var inserted = 0;
        var updated = 0;
        var resourceCodes = new List<string>();

        foreach (var item in distinctItems)
        {
            resourceCodes.Add(item.ResourceCode);

            var existing = await _dbContext.Resources
                .FirstOrDefaultAsync(r => r.ResourceCode == item.ResourceCode, cancellationToken);

            if (existing is null)
            {
                var segments = item.ResourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                _dbContext.Resources.Add(new Resource
                {
                    Id = Guid.NewGuid(),
                    ResourceCode = item.ResourceCode,
                    SystemCode = segments.Length > 0 ? segments[0] : string.Empty,
                    ModuleCode = ResolveModuleCode(item.ResourceCode),
                    ResourceName = item.ResourceName,
                    ResourceType = item.ResourceType,
                    Route = item.Route,
                    Description = item.Description,
                    IsVisible = true,
                    IsEnabled = true,
                    CreateTime = DateTime.Now,
                    CreateUser = actor
                });
                inserted++;
                await DisableLegacyCodeIfAnyAsync(item.ResourceCode, actor, cancellationToken);
                continue;
            }

            existing.ResourceName = item.ResourceName;
            existing.ResourceType = item.ResourceType;
            existing.Route = item.Route;
            existing.Description = item.Description;
            existing.ModuleCode = ResolveModuleCode(item.ResourceCode);
            existing.UpdateTime = DateTime.Now;
            existing.UpdateUser = actor;
            updated++;

            await DisableLegacyCodeIfAnyAsync(item.ResourceCode, actor, cancellationToken);
        }

        await RepairCorruptedResourceNamesAsync(distinctItems, actor, cancellationToken);

        var (disabledLegacyCount, migratedRolePermissionCount) =
            await MigrateAndDisableAllLegacyResourcesAsync(actor, cancellationToken);

        var (disabledIcpPermissionCount, migratedFromIcpPermissionCount) =
            await MigrateAndDisableIcpPermissionResourcesAsync(actor, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionScanResult
        {
            ScannedCount = scannedItems.Count,
            InsertedCount = inserted,
            UpdatedCount = updated,
            DisabledLegacyCount = disabledLegacyCount + disabledIcpPermissionCount,
            MigratedRolePermissionCount = migratedRolePermissionCount + migratedFromIcpPermissionCount,
            ResourceCodes = resourceCodes
        };
    }

    private static string ResolveModuleCode(string resourceCode)
    {
        var segments = resourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 3 &&
            segments[0].Equals("Views", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("Permission", StringComparison.OrdinalIgnoreCase))
        {
            return segments[2];
        }

        if (segments.Length >= 2 &&
            segments[0].Equals("icp", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("permission", StringComparison.OrdinalIgnoreCase))
        {
            return "permission";
        }

        return segments.Length > 1 ? segments[1] : string.Empty;
    }

    private static string? ToLegacyResourceCode(string resourceCode)
    {
        if (!resourceCode.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return LegacyPrefix + resourceCode[PermissionPrefix.Length..];
    }

    private static string? ToPermissionResourceCode(string resourceCode)
    {
        if (resourceCode.StartsWith(PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceCode;
        }

        if (!resourceCode.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return PermissionPrefix + resourceCode[LegacyPrefix.Length..];
    }

    private static string? ToViewsPermissionResourceCode(string resourceCode)
    {
        if (resourceCode.StartsWith(ViewsPermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceCode;
        }

        if (IcpPermissionToViewsPermission.TryGetValue(resourceCode, out var viewsCode))
        {
            return viewsCode;
        }

        var permissionCode = ToPermissionResourceCode(resourceCode);
        if (!string.IsNullOrWhiteSpace(permissionCode) &&
            IcpPermissionToViewsPermission.TryGetValue(permissionCode, out viewsCode))
        {
            return viewsCode;
        }

        return null;
    }

    private async Task DisableLegacyCodeIfAnyAsync(
        string resourceCode,
        string actor,
        CancellationToken cancellationToken)
    {
        if (resourceCode.StartsWith(ViewsPermissionPrefix, StringComparison.OrdinalIgnoreCase) &&
            ViewsPermissionToIcpPermission.TryGetValue(resourceCode, out var icpPermissionCode))
        {
            await DisableResourceByCodeAsync(icpPermissionCode, actor, cancellationToken);

            var legacyCode = ToLegacyResourceCode(icpPermissionCode);
            if (!string.IsNullOrWhiteSpace(legacyCode))
            {
                await DisableResourceByCodeAsync(legacyCode, actor, cancellationToken);
            }

            return;
        }

        var legacyCodeFromPermission = ToLegacyResourceCode(resourceCode);
        if (!string.IsNullOrWhiteSpace(legacyCodeFromPermission))
        {
            await DisableResourceByCodeAsync(legacyCodeFromPermission, actor, cancellationToken);
        }
    }

    private async Task DisableResourceByCodeAsync(
        string resourceCode,
        string actor,
        CancellationToken cancellationToken)
    {
        var legacy = await _dbContext.Resources
            .FirstOrDefaultAsync(r => r.ResourceCode == resourceCode, cancellationToken);

        if (legacy is null || !legacy.IsEnabled)
        {
            return;
        }

        legacy.IsEnabled = false;
        legacy.UpdateTime = DateTime.Now;
        legacy.UpdateUser = actor;
    }

    private async Task RepairCorruptedResourceNamesAsync(
        IReadOnlyList<ScannedPermission> scannedItems,
        string actor,
        CancellationToken cancellationToken)
    {
        var nameByCode = scannedItems.ToDictionary(
            x => x.ResourceCode,
            x => x.ResourceName,
            StringComparer.OrdinalIgnoreCase);

        var corrupted = await _dbContext.Resources
            .Where(r => r.ResourceName.StartsWith("@Localizer[") || r.ResourceName == "@Localizer[")
            .ToListAsync(cancellationToken);

        foreach (var resource in corrupted)
        {
            if (nameByCode.TryGetValue(resource.ResourceCode, out var scannedName) &&
                !string.IsNullOrWhiteSpace(scannedName))
            {
                resource.ResourceName = scannedName;
            }
            else
            {
                var viewsCode = ToViewsPermissionResourceCode(resource.ResourceCode);
                if (!string.IsNullOrWhiteSpace(viewsCode) &&
                    nameByCode.TryGetValue(viewsCode, out scannedName) &&
                    !string.IsNullOrWhiteSpace(scannedName))
                {
                    resource.ResourceName = scannedName;
                }
                else
                {
                    var permissionCode = ToPermissionResourceCode(resource.ResourceCode);
                    if (!string.IsNullOrWhiteSpace(permissionCode) &&
                        nameByCode.TryGetValue(permissionCode, out scannedName) &&
                        !string.IsNullOrWhiteSpace(scannedName))
                    {
                        resource.ResourceName = scannedName;
                    }
                }
            }

            resource.UpdateTime = DateTime.Now;
            resource.UpdateUser = actor;
        }
    }

    private async Task<(int DisabledLegacyCount, int MigratedRolePermissionCount)> MigrateAndDisableAllLegacyResourcesAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var legacyResources = await _dbContext.Resources
            .Where(r => r.IsEnabled &&
                        r.ResourceCode.StartsWith(LegacyPrefix) &&
                        !r.ResourceCode.StartsWith(PermissionPrefix))
            .ToListAsync(cancellationToken);

        if (legacyResources.Count == 0)
        {
            return (0, 0);
        }

        var permissionCodes = legacyResources
            .Select(r => ToPermissionResourceCode(r.ResourceCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Cast<string>()
            .ToList();

        var newResources = await _dbContext.Resources
            .Where(r => r.IsEnabled && permissionCodes.Contains(r.ResourceCode))
            .ToListAsync(cancellationToken);

        return await MigrateRolePermissionsAsync(legacyResources, newResources, actor, cancellationToken);
    }

    private async Task<(int DisabledLegacyCount, int MigratedRolePermissionCount)> MigrateAndDisableIcpPermissionResourcesAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var icpPermissionResources = await _dbContext.Resources
            .Where(r => r.IsEnabled && r.ResourceCode.StartsWith(PermissionPrefix))
            .ToListAsync(cancellationToken);

        if (icpPermissionResources.Count == 0)
        {
            return (0, 0);
        }

        var viewsCodes = icpPermissionResources
            .Select(r => ToViewsPermissionResourceCode(r.ResourceCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Cast<string>()
            .ToList();

        var viewsResources = await _dbContext.Resources
            .Where(r => r.IsEnabled && viewsCodes.Contains(r.ResourceCode))
            .ToListAsync(cancellationToken);

        return await MigrateRolePermissionsAsync(icpPermissionResources, viewsResources, actor, cancellationToken);
    }

    private async Task<(int DisabledLegacyCount, int MigratedRolePermissionCount)> MigrateRolePermissionsAsync(
        IReadOnlyList<Resource> legacyResources,
        IReadOnlyList<Resource> newResources,
        string actor,
        CancellationToken cancellationToken)
    {
        if (legacyResources.Count == 0)
        {
            return (0, 0);
        }

        var newResourceByLegacyCode = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);
        foreach (var legacy in legacyResources)
        {
            var viewsCode = ToViewsPermissionResourceCode(legacy.ResourceCode);
            if (!string.IsNullOrWhiteSpace(viewsCode))
            {
                var match = newResources.FirstOrDefault(r =>
                    r.ResourceCode.Equals(viewsCode, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    newResourceByLegacyCode[legacy.ResourceCode] = match;
                    continue;
                }
            }

            var permissionCode = ToPermissionResourceCode(legacy.ResourceCode);
            if (!string.IsNullOrWhiteSpace(permissionCode))
            {
                var match = newResources.FirstOrDefault(r =>
                    r.ResourceCode.Equals(permissionCode, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    newResourceByLegacyCode[legacy.ResourceCode] = match;
                }
            }
        }

        var legacyIds = legacyResources.Select(r => r.Id).ToList();
        var legacyRolePermissions = await _dbContext.RolePermissions
            .Where(rp => legacyIds.Contains(rp.ResourceId))
            .ToListAsync(cancellationToken);

        var existingKeys = await _dbContext.RolePermissions
            .AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.ResourceId, rp.ActionCode })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(k => $"{k.RoleId}|{k.ResourceId}|{k.ActionCode}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var migratedCount = 0;

        foreach (var legacy in legacyResources)
        {
            newResourceByLegacyCode.TryGetValue(legacy.ResourceCode, out var newResource);

            var rolePermissionsForLegacy = legacyRolePermissions
                .Where(rp => rp.ResourceId == legacy.Id)
                .ToList();

            foreach (var rolePermission in rolePermissionsForLegacy)
            {
                if (newResource is not null)
                {
                    var key = $"{rolePermission.RoleId}|{newResource.Id}|{rolePermission.ActionCode}";
                    if (!existingSet.Contains(key))
                    {
                        var migrated = new RolePermission
                        {
                            Id = Guid.NewGuid(),
                            RoleId = rolePermission.RoleId,
                            ResourceId = newResource.Id,
                            ActionCode = rolePermission.ActionCode,
                            IsAllowed = rolePermission.IsAllowed,
                            DataScope = rolePermission.DataScope,
                            Description = rolePermission.Description
                        };
                        CrudAuditHelper.ApplyCreateAudit(migrated, actor);
                        _dbContext.RolePermissions.Add(migrated);
                        existingSet.Add(key);
                        migratedCount++;
                    }
                }

                _dbContext.RolePermissions.Remove(rolePermission);
            }

            legacy.IsEnabled = false;
            legacy.UpdateTime = DateTime.Now;
            legacy.UpdateUser = actor;
        }

        return (legacyResources.Count, migratedCount);
    }
}
