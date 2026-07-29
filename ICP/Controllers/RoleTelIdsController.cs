using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Models.Ilc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

[PermissionModule]
public class RoleTelIdsController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "TelId",
        "RoleCode",
        "RoleName",
        "Description",
        "CreateTime",
        "CreateUser"
    };

    private static readonly HashSet<string> AllowedRolePickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName"
    };

    private static readonly HashSet<string> AllowedUserPickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "TelId",
        "UserName",
        "DisplayName",
        "DepName",
        "EmailAddress"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly IlcDbContext _ilcDb;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RoleTelIdsController(
        ApplicationDbContext icpDb,
        IlcDbContext ilcDb,
        IStringLocalizer<SharedResource> localizer)
    {
        _icpDb = icpDb;
        _ilcDb = ilcDb;
        _localizer = localizer;
    }

    public IActionResult Index()
    {
        return View("View");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchCreate(
        [FromBody] RoleTelIdsBatchCreateModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.RoleIds.Count == 0 || model.TelIds.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRoleAndUser"]);
        }

        var roleIds = model.RoleIds.Distinct().ToList();
        var telIds = model.TelIds
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _icpDb.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (roles.Count == 0 || telIds.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.ValidRoleUserNotFound"]);
        }

        var validRoleIds = roles.Select(r => r.Id).ToHashSet();
        var existingKeys = await _icpDb.RolesTelId
            .AsNoTracking()
            .Where(r => validRoleIds.Contains(r.RoleId) && telIds.Contains(r.TelId))
            .Select(r => new { r.RoleId, r.TelId })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(k => $"{k.RoleId}|{k.TelId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped = 0;
        var actor = User.Identity?.Name;

        foreach (var role in roles)
        {
            foreach (var telId in telIds)
            {
                var key = $"{role.Id}|{telId}";
                if (existingSet.Contains(key))
                {
                    skipped++;
                    continue;
                }

                var entity = new RoleTelId
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    TelId = telId,
                    IsEnabled = true
                };
                CrudAuditHelper.ApplyCreateAudit(entity, actor);
                _icpDb.RolesTelId.Add(entity);
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
        [FromBody] RoleTelIdsBatchDeleteModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRecords"]);
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.RolesTelId
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        _icpDb.RolesTelId.RemoveRange(entities);
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
    public async Task<IActionResult> QueryUsers([FromForm] UsersSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryUsersPickAsync(criteria, cancellationToken);
        return PartialView("View.UsersPickList", new UsersSearchListViewModel { ListData = list });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptionsUsers(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedUserPickFilterColumns.Contains(column))
        {
            return BadRequest();
        }

        var query = _ilcDb.UserInfoAd.AsNoTracking().Where(u => u.TelId != null && u.TelId != "");

        var options = column switch
        {
            "TelId" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.TelId), search, cancellationToken),
            "UserName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.UserName), search, cancellationToken),
            "DisplayName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.DisplayName), search, cancellationToken),
            "DepName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.DepName), search, cancellationToken),
            "EmailAddress" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.EmailAddress), search, cancellationToken),
            _ => []
        };

        return Json(options);
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] RoleTelIdsSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRoleTelIdsAsync(criteria, cancellationToken);
        return PartialView("View.List", new RoleTelIdsSearchListViewModel { ListData = list });
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

    private IQueryable<RoleTelId> BaseQuery()
    {
        return _icpDb.RolesTelId
            .AsNoTracking()
            .Include(r => r.Role);
    }

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        return column switch
        {
            "TelId" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.TelId), search, cancellationToken),
            "RoleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleCode), search, cancellationToken),
            "RoleName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleName), search, cancellationToken),
            "Description" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Description), search, cancellationToken),
            "CreateTime" => await SearchFilterHelper.DistinctDateTimeAsync(query.Select(r => r.CreateTime), search, cancellationToken),
            "CreateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.CreateUser), search, cancellationToken),
            _ => []
        };
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

    private async Task<List<UserInfoAd>> QueryUsersPickAsync(UsersSearchModel criteria, CancellationToken cancellationToken)
    {
        var query = _ilcDb.UserInfoAd.AsNoTracking().Where(u => u.TelId != null && u.TelId != "");

        if (criteria.TelIds.Count > 0)
        {
            query = query.Where(u => u.TelId != null && criteria.TelIds.Contains(u.TelId));
        }

        if (criteria.UserNames.Count > 0)
        {
            query = query.Where(u => u.UserName != null && criteria.UserNames.Contains(u.UserName));
        }

        if (criteria.DisplayNames.Count > 0)
        {
            query = query.Where(u => u.DisplayName != null && criteria.DisplayNames.Contains(u.DisplayName));
        }

        if (criteria.DepNames.Count > 0)
        {
            query = query.Where(u => u.DepName != null && criteria.DepNames.Contains(u.DepName));
        }

        if (criteria.EmailAddresses.Count > 0)
        {
            query = query.Where(u => u.EmailAddress != null && criteria.EmailAddresses.Contains(u.EmailAddress));
        }

        return await query
            .OrderBy(u => u.TelId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RoleTelId>> QueryRoleTelIdsAsync(
        RoleTelIdsSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.TelIds.Count > 0)
        {
            query = query.Where(r => criteria.TelIds.Contains(r.TelId));
        }

        if (criteria.RoleCodes.Count > 0)
        {
            query = query.Where(r => criteria.RoleCodes.Contains(r.Role.RoleCode));
        }

        if (criteria.RoleNames.Count > 0)
        {
            query = query.Where(r => criteria.RoleNames.Contains(r.Role.RoleName));
        }

        if (criteria.Descriptions.Count > 0)
        {
            query = query.Where(r => r.Description != null && criteria.Descriptions.Contains(r.Description));
        }

        if (criteria.CreateTimes.Count > 0)
        {
            var createTimes = criteria.CreateTimes
                .Select(v => DateTime.TryParse(v, out var dt) ? (DateTime?)dt : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (createTimes.Count > 0)
            {
                query = query.Where(r => createTimes.Contains(r.CreateTime));
            }
        }

        if (criteria.CreateUsers.Count > 0)
        {
            query = query.Where(r => r.CreateUser != null && criteria.CreateUsers.Contains(r.CreateUser));
        }

        return await query
            .OrderBy(r => r.TelId)
            .ThenBy(r => r.Role.RoleCode)
            .ToListAsync(cancellationToken);
    }
}
