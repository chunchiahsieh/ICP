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
public class RoleMailGroupsController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "MailGroupAddress",
        "MailGroupName",
        "RoleCode",
        "RoleName",
        "CreateTime",
        "CreateUser"
    };

    private static readonly HashSet<string> AllowedRolePickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName"
    };

    private static readonly HashSet<string> AllowedMailGroupPickFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Uid",
        "Name",
        "Address"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly FiestaDbContext _fiestaDb;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RoleMailGroupsController(
        ApplicationDbContext icpDb,
        FiestaDbContext fiestaDb,
        IStringLocalizer<SharedResource> localizer)
    {
        _icpDb = icpDb;
        _fiestaDb = fiestaDb;
        _localizer = localizer;
    }

    public IActionResult Index()
    {
        return View("View");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchCreate(
        [FromBody] RoleMailGroupsBatchCreateModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.RoleIds.Count == 0 || model.MailGroupAddresses.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRoleAndMailGroup"]);
        }

        var roleIds = model.RoleIds.Distinct().ToList();
        var addresses = model.MailGroupAddresses
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _icpDb.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (roles.Count == 0 || addresses.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.ValidRoleMailGroupNotFound"]);
        }

        var validAddresses = await _fiestaDb.MailGroup
            .AsNoTracking()
            .Where(m => m.Address != null && addresses.Contains(m.Address))
            .Select(m => m.Address!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (validAddresses.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.ValidRoleMailGroupNotFound"]);
        }

        var validRoleIds = roles.Select(r => r.Id).ToHashSet();
        var existingKeys = await _icpDb.RolesMailGroup
            .AsNoTracking()
            .Where(r => validRoleIds.Contains(r.RoleId) && validAddresses.Contains(r.Address))
            .Select(r => new { r.RoleId, r.Address })
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys
            .Select(k => $"{k.RoleId}|{k.Address}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped = 0;
        var actor = User.Identity?.Name;

        foreach (var role in roles)
        {
            foreach (var address in validAddresses)
            {
                var key = $"{role.Id}|{address}";
                if (existingSet.Contains(key))
                {
                    skipped++;
                    continue;
                }

                var entity = new RoleMailGroup
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Address = address,
                    IsEnabled = true
                };
                CrudAuditHelper.ApplyCreateAudit(entity, actor);
                _icpDb.RolesMailGroup.Add(entity);
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
        [FromBody] RoleMailGroupsBatchDeleteModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRecords"]);
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.RolesMailGroup
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        _icpDb.RolesMailGroup.RemoveRange(entities);
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
    public async Task<IActionResult> QueryMailGroups([FromForm] MailGroupsSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryMailGroupsPickAsync(criteria, cancellationToken);
        return PartialView("View.MailGroupsPickList", new MailGroupsSearchListViewModel { ListData = list });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptionsMailGroups(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedMailGroupPickFilterColumns.Contains(column))
        {
            return BadRequest();
        }

        var query = _fiestaDb.MailGroup.AsNoTracking();

        var options = column switch
        {
            "Uid" => await SearchFilterHelper.DistinctNonEmptyAsync(
                query.Select(m => m.Uid.ToString()),
                search,
                cancellationToken),
            "Name" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(m => m.Name), search, cancellationToken),
            "Address" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(m => m.Address), search, cancellationToken),
            _ => []
        };

        return Json(options);
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] RoleMailGroupsSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRoleMailGroupsAsync(criteria, cancellationToken);
        return PartialView("View.List", new RoleMailGroupsSearchListViewModel { ListData = list });
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

    private IQueryable<RoleMailGroup> BaseQuery()
    {
        return _icpDb.RolesMailGroup
            .AsNoTracking()
            .Include(r => r.Role);
    }

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var items = await QueryRoleMailGroupsAsync(new RoleMailGroupsSearchModel(), cancellationToken);

        IEnumerable<string?> values = column switch
        {
            "MailGroupAddress" => items.Select(i => i.MailGroupAddress),
            "MailGroupName" => items.Select(i => i.MailGroupName),
            "RoleCode" => items.Select(i => i.RoleCode),
            "RoleName" => items.Select(i => i.RoleName),
            "CreateTime" => items.Select(i => i.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            "CreateUser" => items.Select(i => i.CreateUser),
            _ => []
        };

        var query = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(v => v.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(v => v)
            .Take(SearchFilterHelper.FilterOptionsLimit)
            .ToList();
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

    private async Task<List<MailGroupPickItem>> QueryMailGroupsPickAsync(
        MailGroupsSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = _fiestaDb.MailGroup.AsNoTracking();

        if (criteria.Uids.Count > 0)
        {
            var uids = criteria.Uids
                .Select(s => int.TryParse(s?.Trim(), out var uid) ? uid : 0)
                .Where(uid => uid > 0)
                .ToList();

            if (uids.Count > 0)
            {
                query = query.Where(m => uids.Contains(m.Uid));
            }
        }

        if (criteria.Names.Count > 0)
        {
            query = query.Where(m => m.Name != null && criteria.Names.Contains(m.Name));
        }

        if (criteria.Addresses.Count > 0)
        {
            query = query.Where(m => m.Address != null && criteria.Addresses.Contains(m.Address));
        }

        var mailGroups = await query
            .OrderBy(m => m.Uid)
            .ToListAsync(cancellationToken);

        return mailGroups.Select(m => new MailGroupPickItem
        {
            Uid = m.Uid,
            Name = m.Name,
            Address = m.Address
        }).ToList();
    }

    private async Task<List<RoleMailGroupListItem>> QueryRoleMailGroupsAsync(
        RoleMailGroupsSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.MailGroupAddresses.Count > 0)
        {
            query = query.Where(r => criteria.MailGroupAddresses.Contains(r.Address));
        }

        if (criteria.RoleCodes.Count > 0)
        {
            query = query.Where(r => criteria.RoleCodes.Contains(r.Role.RoleCode));
        }

        if (criteria.RoleNames.Count > 0)
        {
            query = query.Where(r => criteria.RoleNames.Contains(r.Role.RoleName));
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

        var assignments = await query
            .OrderBy(r => r.Address)
            .ThenBy(r => r.Role.RoleCode)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return [];
        }

        var addresses = assignments.Select(a => a.Address).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var mailGroups = await _fiestaDb.MailGroup
            .AsNoTracking()
            .Where(m => m.Address != null && addresses.Contains(m.Address))
            .ToListAsync(cancellationToken);

        var mailGroupByAddress = mailGroups
            .Where(m => !string.IsNullOrWhiteSpace(m.Address))
            .GroupBy(m => m.Address!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var items = assignments.Select(a =>
        {
            mailGroupByAddress.TryGetValue(a.Address, out var mailGroup);
            return new RoleMailGroupListItem
            {
                Id = a.Id,
                MailGroupAddress = a.Address,
                MailGroupName = mailGroup?.Name,
                RoleCode = a.Role.RoleCode,
                RoleName = a.Role.RoleName,
                CreateTime = a.CreateTime,
                CreateUser = a.CreateUser
            };
        }).ToList();

        if (criteria.MailGroupNames.Count > 0)
        {
            items = items
                .Where(i => i.MailGroupName != null && criteria.MailGroupNames.Contains(i.MailGroupName))
                .ToList();
        }

        if (criteria.MailGroupAddresses.Count > 0)
        {
            items = items
                .Where(i => criteria.MailGroupAddresses.Contains(i.MailGroupAddress))
                .ToList();
        }

        return items;
    }
}
