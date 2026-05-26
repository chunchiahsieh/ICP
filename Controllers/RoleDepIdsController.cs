using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Models.Ilc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ICP.Controllers;

public class RoleDepIdsController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DepId",
        "RoleCode",
        "RoleName",
        "Description",
        "CreateTime",
        "CreateUser",
        "UpdateTime",
        "UpdateUser"
    };

    private static readonly HashSet<string> AllowedRolePickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName"
    };

    private static readonly HashSet<string> AllowedUserPickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DepId",
        "DepName",
        "DisplayName",
        "UserName"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly IlcDbContext _ilcDb;

    public RoleDepIdsController(ApplicationDbContext icpDb, IlcDbContext ilcDb)
    {
        _icpDb = icpDb;
        _ilcDb = ilcDb;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchCreate(
        [FromBody] RoleDepIdsBatchCreateModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.RoleIds.Count == 0 || model.DepIds.Count == 0)
        {
            return CrudJsonHelper.Failure("請至少選擇一筆角色與一筆部門");
        }

        var roleIds = model.RoleIds.Distinct().ToList();
        var depIds = model.DepIds
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _icpDb.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (roles.Count == 0 || depIds.Count == 0)
        {
            return CrudJsonHelper.Failure("找不到有效的角色或部門");
        }

        var validRoleIds = roles.Select(r => r.Id).ToHashSet();
        var existingKeys = await _icpDb.RolesDepId
            .AsNoTracking()
            .Where(r => validRoleIds.Contains(r.RoleId) && depIds.Contains(r.DepId))
            .Select(r => new { r.RoleId, r.DepId })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(k => $"{k.RoleId}|{k.DepId}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped = 0;
        var actor = User.Identity?.Name;

        foreach (var role in roles)
        {
            foreach (var depId in depIds)
            {
                var key = $"{role.Id}|{depId}";
                if (existingSet.Contains(key))
                {
                    skipped++;
                    continue;
                }

                var entity = new RoleDepId
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    DepId = depId,
                    IsEnabled = true
                };
                CrudAuditHelper.ApplyCreateAudit(entity, actor);
                _icpDb.RolesDepId.Add(entity);
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
        [FromBody] RoleDepIdsBatchDeleteModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure("請至少選擇一筆資料");
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.RolesDepId
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure("找不到資料");
        }

        _icpDb.RolesDepId.RemoveRange(entities);
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
        return PartialView("_RolesPickList", new RolesSearchListViewModel { ListData = list });
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
        return PartialView("_UsersPickList", new UsersSearchListViewModel { ListData = list });
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

        var query = _ilcDb.UserInfoAd.AsNoTracking().Where(u => u.DepId != null && u.DepId != "");

        var options = column switch
        {
            "DepId" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.DepId), search, cancellationToken),
            "DepName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.DepName), search, cancellationToken),
            "DisplayName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.DisplayName), search, cancellationToken),
            "UserName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(u => u.UserName), search, cancellationToken),
            _ => []
        };

        return Json(options);
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] RoleDepIdsSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRoleDepIdsAsync(criteria, cancellationToken);
        return PartialView("_SearchList", new RoleDepIdsSearchListViewModel { ListData = list });
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

    private IQueryable<RoleDepId> BaseQuery()
    {
        return _icpDb.RolesDepId
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
            "DepId" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.DepId), search, cancellationToken),
            "RoleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleCode), search, cancellationToken),
            "RoleName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Role.RoleName), search, cancellationToken),
            "Description" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Description), search, cancellationToken),
            "CreateTime" => await SearchFilterHelper.DistinctDateTimeAsync(query.Select(r => r.CreateTime), search, cancellationToken),
            "CreateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.CreateUser), search, cancellationToken),
            "UpdateTime" => await SearchFilterHelper.DistinctNullableDateTimeAsync(query.Select(r => r.UpdateTime), search, cancellationToken),
            "UpdateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.UpdateUser), search, cancellationToken),
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
        var query = _ilcDb.UserInfoAd.AsNoTracking().Where(u => u.DepId != null && u.DepId != "");

        if (criteria.DepIds.Count > 0)
        {
            query = query.Where(u => u.DepId != null && criteria.DepIds.Contains(u.DepId));
        }

        if (criteria.DepNames.Count > 0)
        {
            query = query.Where(u => u.DepName != null && criteria.DepNames.Contains(u.DepName));
        }

        if (criteria.DisplayNames.Count > 0)
        {
            query = query.Where(u => u.DisplayName != null && criteria.DisplayNames.Contains(u.DisplayName));
        }

        if (criteria.UserNames.Count > 0)
        {
            query = query.Where(u => u.UserName != null && criteria.UserNames.Contains(u.UserName));
        }

        return await query
            .OrderBy(u => u.DepId)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RoleDepId>> QueryRoleDepIdsAsync(
        RoleDepIdsSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.DepIds.Count > 0)
        {
            query = query.Where(r => criteria.DepIds.Contains(r.DepId));
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

        if (criteria.UpdateTimes.Count > 0)
        {
            var updateTimes = criteria.UpdateTimes
                .Select(v => DateTime.TryParse(v, out var dt) ? (DateTime?)dt : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (updateTimes.Count > 0)
            {
                query = query.Where(r => r.UpdateTime.HasValue && updateTimes.Contains(r.UpdateTime.Value));
            }
        }

        if (criteria.UpdateUsers.Count > 0)
        {
            query = query.Where(r => r.UpdateUser != null && criteria.UpdateUsers.Contains(r.UpdateUser));
        }

        return await query
            .OrderBy(r => r.DepId)
            .ThenBy(r => r.Role.RoleCode)
            .ToListAsync(cancellationToken);
    }
}
