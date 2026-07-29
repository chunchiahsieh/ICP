using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

[PermissionModule]
public class RolePermissionsController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName",
        "ResourceCode",
        "ResourceName",
        "ResourceType",
        "ActionCode"
    };

    private static readonly HashSet<string> AllowedRolePickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName"
    };

    private static readonly HashSet<string> AllowedResourcePickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ResourceCode",
        "ResourceName",
        "ResourceType",
        "SystemCode",
        "ModuleCode"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RolePermissionsController(
        ApplicationDbContext icpDb,
        IStringLocalizer<SharedResource> localizer)
    {
        _icpDb = icpDb;
        _localizer = localizer;
    }

    public IActionResult Index()
    {
        return View("View");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchCreate(
        [FromBody] RolePermissionBatchCreateModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.RoleIds.Count == 0 || model.ResourceIds.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRoleAndResource"]);
        }

        var roleIds = model.RoleIds.Distinct().ToList();
        var resourceIds = model.ResourceIds.Distinct().ToList();

        var roles = await _icpDb.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        var resources = await _icpDb.Resources
            .AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (roles.Count == 0 || resources.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.ValidRoleResourceNotFound"]);
        }

        var validRoleIds = roles.Select(r => r.Id).ToHashSet();
        var existingKeys = await _icpDb.RolePermissions
            .AsNoTracking()
            .Where(rp => validRoleIds.Contains(rp.RoleId) && resourceIds.Contains(rp.ResourceId))
            .Select(rp => new { rp.RoleId, rp.ResourceId, rp.ActionCode })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(k => $"{k.RoleId}|{k.ResourceId}|{k.ActionCode}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped = 0;
        var actor = User.Identity?.Name;

        foreach (var role in roles)
        {
            foreach (var resource in resources)
            {
                var actionCode = RolePermissionActionCodes.Resolve(resource);
                var key = $"{role.Id}|{resource.Id}|{actionCode}";
                if (existingSet.Contains(key))
                {
                    skipped++;
                    continue;
                }

                var entity = new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    ResourceId = resource.Id,
                    ActionCode = actionCode,
                    IsAllowed = true
                };
                CrudAuditHelper.ApplyCreateAudit(entity, actor);
                _icpDb.RolePermissions.Add(entity);
                existingSet.Add(key);
                inserted++;
            }
        }

        if (inserted > 0)
        {
            await _icpDb.SaveChangesAsync(cancellationToken);
        }

        return new JsonResult(new
        {
            success = true,
            insertedCount = inserted,
            skippedCount = skipped
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchDelete(
        [FromBody] RolePermissionsBatchDeleteModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRecords"]);
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.RolePermissions
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        _icpDb.RolePermissions.RemoveRange(entities);
        await _icpDb.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
            success = true,
            deletedCount = entities.Count
        });
    }

    [HttpPost]
    public async Task<IActionResult> QueryRoles([FromForm] RolesSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRolesPickAsync(criteria, cancellationToken);
        return PartialView("View.RolesPickList", new RolesSearchListViewModel { ListData = list });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptionsRoles(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedRolePickFilterColumns.Contains(column))
        {
            return BadRequest();
        }

        var query = _icpDb.Roles.AsNoTracking().Where(r => r.IsEnabled);

        var options = column switch
        {
            "RoleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.RoleCode), search, cancellationToken),
            "RoleName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.RoleName), search, cancellationToken),
            _ => []
        };

        return Json(options);
    }

    [HttpPost]
    public async Task<IActionResult> QueryResources([FromForm] ResourcesSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryResourcesPickAsync(criteria, cancellationToken);
        return PartialView("View.ResourcesPickList", new ResourcesSearchListViewModel { ListData = list });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptionsResources(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedResourcePickFilterColumns.Contains(column))
        {
            return BadRequest();
        }

        var query = _icpDb.Resources.AsNoTracking().Where(r => r.IsEnabled);

        var options = column switch
        {
            "ResourceCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceCode), search, cancellationToken),
            "ResourceName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceName), search, cancellationToken),
            "ResourceType" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceType), search, cancellationToken),
            "SystemCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.SystemCode), search, cancellationToken),
            "ModuleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ModuleCode), search, cancellationToken),
            _ => []
        };

        return Json(options);
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] RolePermissionsSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRolePermissionsAsync(criteria, cancellationToken);
        return PartialView("View.List", new RolePermissionsSearchListViewModel { ListData = list });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptions(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedFilterColumns.Contains(column))
        {
            return BadRequest();
        }

        var options = await GetDistinctColumnValuesAsync(column, search, cancellationToken);
        return Json(options);
    }

    private async Task<List<Role>> QueryRolesPickAsync(RolesSearchModel criteria, CancellationToken cancellationToken)
    {
        var query = _icpDb.Roles.AsNoTracking().Where(r => r.IsEnabled);

        if (criteria.RoleCodes.Count > 0)
        {
            query = query.Where(r => criteria.RoleCodes.Contains(r.RoleCode));
        }

        if (criteria.RoleNames.Count > 0)
        {
            query = query.Where(r => criteria.RoleNames.Contains(r.RoleName));
        }

        return await query
            .OrderBy(r => r.RoleCode)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Resource>> QueryResourcesPickAsync(ResourcesSearchModel criteria, CancellationToken cancellationToken)
    {
        var query = _icpDb.Resources.AsNoTracking().Where(r => r.IsEnabled);

        if (criteria.ResourceCodes.Count > 0)
        {
            query = query.Where(r => criteria.ResourceCodes.Contains(r.ResourceCode));
        }

        if (criteria.ResourceNames.Count > 0)
        {
            query = query.Where(r => criteria.ResourceNames.Contains(r.ResourceName));
        }

        if (criteria.ResourceTypes.Count > 0)
        {
            query = query.Where(r => criteria.ResourceTypes.Contains(r.ResourceType));
        }

        if (criteria.SystemCodes.Count > 0)
        {
            query = query.Where(r => criteria.SystemCodes.Contains(r.SystemCode));
        }

        if (criteria.ModuleCodes.Count > 0)
        {
            query = query.Where(r => criteria.ModuleCodes.Contains(r.ModuleCode));
        }

        return await query
            .OrderBy(r => r.Sort)
            .ThenBy(r => r.ResourceName)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<RolePermission> BaseQuery()
    {
        return _icpDb.RolePermissions
            .AsNoTracking()
            .Include(r => r.Role)
            .Include(r => r.Resource);
    }

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        return column switch
        {
            "RoleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleCode), search, cancellationToken),
            "RoleName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleName), search, cancellationToken),
            "ResourceCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Resource.ResourceCode), search, cancellationToken),
            "ResourceName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Resource.ResourceName), search, cancellationToken),
            "ResourceType" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Resource.ResourceType), search, cancellationToken),
            "ActionCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ActionCode), search, cancellationToken),
            _ => []
        };
    }

    private async Task<List<RolePermission>> QueryRolePermissionsAsync(
        RolePermissionsSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.RoleCodes.Count > 0)
        {
            query = query.Where(r => criteria.RoleCodes.Contains(r.Role.RoleCode));
        }

        if (criteria.RoleNames.Count > 0)
        {
            query = query.Where(r => criteria.RoleNames.Contains(r.Role.RoleName));
        }

        if (criteria.ResourceCodes.Count > 0)
        {
            query = query.Where(r => criteria.ResourceCodes.Contains(r.Resource.ResourceCode));
        }

        if (criteria.ResourceNames.Count > 0)
        {
            query = query.Where(r => criteria.ResourceNames.Contains(r.Resource.ResourceName));
        }

        if (criteria.ResourceTypes.Count > 0)
        {
            query = query.Where(r => criteria.ResourceTypes.Contains(r.Resource.ResourceType));
        }

        if (criteria.ActionCodes.Count > 0)
        {
            query = query.Where(r => criteria.ActionCodes.Contains(r.ActionCode));
        }

        return await query
            .OrderBy(r => r.Role.RoleCode)
            .ThenBy(r => r.Resource.ResourceCode)
            .ThenBy(r => r.ActionCode)
            .ToListAsync(cancellationToken);
    }
}
