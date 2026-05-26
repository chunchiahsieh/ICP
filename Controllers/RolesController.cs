using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class RolesController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleCode",
        "RoleName",
        "IsEnabled",
        "Description",
        "CreateTime",
        "CreateUser",
        "UpdateTime",
        "UpdateUser"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RolesController(
        ApplicationDbContext icpDb,
        IStringLocalizer<SharedResource> localizer)
    {
        _icpDb = icpDb;
        _localizer = localizer;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(CancellationToken cancellationToken = default)
    {
        var list = await _icpDb.Roles
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.RoleCode)
            .Select(r => new { r.Id, r.RoleCode, r.RoleName })
            .ToListAsync(cancellationToken);

        return Json(list);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _icpDb.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return Json(new RoleEditModel
        {
            Id = entity.Id,
            RoleCode = entity.RoleCode,
            RoleName = entity.RoleName,
            IsEnabled = entity.IsEnabled,
            Description = entity.Description
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Save([FromBody] RoleEditModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return CrudJsonHelper.ValidationErrors(ModelState);
        }

        try
        {
            if (model.Id.HasValue && model.Id.Value != Guid.Empty)
            {
                var entity = await _icpDb.Roles.FirstOrDefaultAsync(r => r.Id == model.Id.Value, cancellationToken);
                if (entity is null)
                {
                    return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
                }

                entity.RoleCode = model.RoleCode.Trim();
                entity.RoleName = model.RoleName.Trim();
                entity.IsEnabled = model.IsEnabled;
                entity.Description = model.Description?.Trim();
                CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
            }
            else
            {
                var entity = new Role
                {
                    Id = Guid.NewGuid(),
                    RoleCode = model.RoleCode.Trim(),
                    RoleName = model.RoleName.Trim(),
                    IsEnabled = model.IsEnabled,
                    Description = model.Description?.Trim()
                };
                CrudAuditHelper.ApplyCreateAudit(entity, User.Identity?.Name);
                _icpDb.Roles.Add(entity);
            }

            await _icpDb.SaveChangesAsync(cancellationToken);
            return CrudJsonHelper.Success();
        }
        catch (DbUpdateException ex)
        {
            var message = CrudAuditHelper.MapDbUpdateException(ex, _localizer) ?? _localizer["Message.SaveFailed"];
            return CrudJsonHelper.Failure(message);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _icpDb.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        entity.IsEnabled = false;
        CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
        await _icpDb.SaveChangesAsync(cancellationToken);
        return CrudJsonHelper.Success();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchDisable(
        [FromBody] RolesBatchDisableModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRecords"]);
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.Roles
            .Where(r => ids.Contains(r.Id) && r.IsEnabled)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        foreach (var entity in entities)
        {
            entity.IsEnabled = false;
            CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
        }

        await _icpDb.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
            success = true,
            disabledCount = entities.Count
        });
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] RolesSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryRolesAsync(criteria, cancellationToken);
        return PartialView("_SearchList", new RolesSearchListViewModel { ListData = list });
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

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _icpDb.Roles.AsNoTracking();

        return column switch
        {
            "RoleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.RoleCode), search, cancellationToken),
            "RoleName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.RoleName), search, cancellationToken),
            "IsEnabled" => await SearchFilterHelper.DistinctBoolAsync(query.Select(r => r.IsEnabled), cancellationToken),
            "Description" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Description), search, cancellationToken),
            "CreateTime" => await SearchFilterHelper.DistinctDateTimeAsync(query.Select(r => r.CreateTime), search, cancellationToken),
            "CreateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.CreateUser), search, cancellationToken),
            "UpdateTime" => await SearchFilterHelper.DistinctNullableDateTimeAsync(query.Select(r => r.UpdateTime), search, cancellationToken),
            "UpdateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.UpdateUser), search, cancellationToken),
            _ => []
        };
    }

    private async Task<List<Role>> QueryRolesAsync(RolesSearchModel criteria, CancellationToken cancellationToken)
    {
        var query = _icpDb.Roles.AsNoTracking();

        if (criteria.RoleCodes.Count > 0)
        {
            query = query.Where(r => criteria.RoleCodes.Contains(r.RoleCode));
        }

        if (criteria.RoleNames.Count > 0)
        {
            query = query.Where(r => criteria.RoleNames.Contains(r.RoleName));
        }

        var isEnableds = SearchFilterHelper.ParseBoolValues(criteria.IsEnableds);
        if (isEnableds.Count > 0)
        {
            query = query.Where(r => isEnableds.Contains(r.IsEnabled));
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
            .OrderBy(r => r.RoleCode)
            .ToListAsync(cancellationToken);
    }
}
