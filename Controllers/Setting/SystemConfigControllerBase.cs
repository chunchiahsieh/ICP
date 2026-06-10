using ICP;
using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public abstract class SystemConfigControllerBase : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Category",
        "Key1",
        "Key2",
        "Value1",
        "Value2",
        "Value3",
        "Value4",
        "Value5",
        "Value6",
        "CreateTime",
        "CreateUser",
        "UpdateTime",
        "UpdateUser"
    };

    private readonly ApplicationDbContext _icpDb;
    private readonly IStringLocalizer<SharedResource> _localizer;

    protected SystemConfigControllerBase(
        ApplicationDbContext icpDb,
        IStringLocalizer<SharedResource> localizer)
    {
        _icpDb = icpDb;
        _localizer = localizer;
    }

    protected abstract string Category { get; }

    protected string PermissionPrefix => $"Views.Setting.{Category}";

    protected string SettingViewPath => $"~/Views/Setting/{Category}/View.cshtml";

    protected string SettingListPartialPath => $"~/Views/Setting/{Category}/View.List.cshtml";

    public IActionResult Index()
    {
        return View(SettingViewPath);
    }

    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _icpDb.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Category == Category && !e.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return Json(MapToEditModel(entity));
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Save([FromBody] SystemConfigEditModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Key1))
        {
            ModelState.AddModelError(nameof(SystemConfigEditModel.Key1), _localizer["Setting.SystemConfig.KeyRequired"]);
        }

        if (!ModelState.IsValid)
        {
            return CrudJsonHelper.ValidationErrors(ModelState);
        }

        try
        {
            if (model.Id > 0)
            {
                var entity = await _icpDb.SystemConfigs
                    .FirstOrDefaultAsync(e => e.Id == model.Id && e.Category == Category && !e.IsDeleted, cancellationToken);

                if (entity is null)
                {
                    return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
                }

                ApplyEditModel(entity, model);
                CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
            }
            else
            {
                var entity = new SystemConfig
                {
                    Category = Category,
                    Key2 = string.Empty,
                    IsDeleted = false
                };
                ApplyEditModel(entity, model);
                CrudAuditHelper.ApplyCreateAudit(entity, User.Identity?.Name);
                _icpDb.SystemConfigs.Add(entity);
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
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _icpDb.SystemConfigs
            .FirstOrDefaultAsync(e => e.Id == id && e.Category == Category && !e.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        entity.IsDeleted = true;
        CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
        await _icpDb.SaveChangesAsync(cancellationToken);
        return CrudJsonHelper.Success();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchDelete(
        [FromBody] SystemConfigBatchDeleteModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.Ids.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.SelectRecords"]);
        }

        var ids = model.Ids.Distinct().ToList();
        var entities = await _icpDb.SystemConfigs
            .Where(e => ids.Contains(e.Id) && e.Category == Category && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return CrudJsonHelper.Failure(_localizer["Message.RecordsNotFound"]);
        }

        foreach (var entity in entities)
        {
            entity.IsDeleted = true;
            CrudAuditHelper.ApplyUpdateAudit(entity, User.Identity?.Name);
        }

        await _icpDb.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
            success = true,
            deletedCount = entities.Count
        });
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] SystemConfigSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryConfigsAsync(criteria, cancellationToken);
        return PartialView(SettingListPartialPath, new SystemConfigSearchListViewModel
        {
            ListData = list,
            PermissionPrefix = PermissionPrefix
        });
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

    protected virtual void ApplyEditModel(SystemConfig entity, SystemConfigEditModel model)
    {
        entity.Key1 = model.Key1!.Trim();
        entity.Value1 = model.Value1?.Trim();
    }

    protected virtual SystemConfigEditModel MapToEditModel(SystemConfig entity)
    {
        return new SystemConfigEditModel
        {
            Id = entity.Id,
            Category = entity.Category,
            Key1 = entity.Key1,
            Value1 = entity.Value1
        };
    }

    private IQueryable<SystemConfig> BaseQuery()
    {
        return _icpDb.SystemConfigs
            .AsNoTracking()
            .Where(e => e.Category == Category && !e.IsDeleted);
    }

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        return column switch
        {
            "Category" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Category), search, cancellationToken),
            "Key1" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Key1), search, cancellationToken),
            "Key2" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Key2), search, cancellationToken),
            "Value1" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value1), search, cancellationToken),
            "Value2" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value2), search, cancellationToken),
            "Value3" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value3), search, cancellationToken),
            "Value4" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value4), search, cancellationToken),
            "Value5" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value5), search, cancellationToken),
            "Value6" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Value6), search, cancellationToken),
            "CreateTime" => await SearchFilterHelper.DistinctDateTimeAsync(query.Select(e => e.CreateTime), search, cancellationToken),
            "CreateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.CreateUser), search, cancellationToken),
            "UpdateTime" => await SearchFilterHelper.DistinctNullableDateTimeAsync(query.Select(e => e.UpdateTime), search, cancellationToken),
            "UpdateUser" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.UpdateUser), search, cancellationToken),
            _ => []
        };
    }

    private async Task<List<SystemConfig>> QueryConfigsAsync(
        SystemConfigSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.Categories.Count > 0)
        {
            query = query.Where(e => criteria.Categories.Contains(e.Category));
        }

        if (criteria.Key1s.Count > 0)
        {
            query = query.Where(e => criteria.Key1s.Contains(e.Key1));
        }

        if (criteria.Key2s.Count > 0)
        {
            query = query.Where(e => criteria.Key2s.Contains(e.Key2));
        }

        if (criteria.Value1s.Count > 0)
        {
            query = query.Where(e => e.Value1 != null && criteria.Value1s.Contains(e.Value1));
        }

        if (criteria.Value2s.Count > 0)
        {
            query = query.Where(e => e.Value2 != null && criteria.Value2s.Contains(e.Value2));
        }

        if (criteria.Value3s.Count > 0)
        {
            query = query.Where(e => e.Value3 != null && criteria.Value3s.Contains(e.Value3));
        }

        if (criteria.Value4s.Count > 0)
        {
            query = query.Where(e => e.Value4 != null && criteria.Value4s.Contains(e.Value4));
        }

        if (criteria.Value5s.Count > 0)
        {
            query = query.Where(e => e.Value5 != null && criteria.Value5s.Contains(e.Value5));
        }

        if (criteria.Value6s.Count > 0)
        {
            query = query.Where(e => e.Value6 != null && criteria.Value6s.Contains(e.Value6));
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
                query = query.Where(e => createTimes.Contains(e.CreateTime));
            }
        }

        if (criteria.CreateUsers.Count > 0)
        {
            query = query.Where(e => e.CreateUser != null && criteria.CreateUsers.Contains(e.CreateUser));
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
                query = query.Where(e => e.UpdateTime.HasValue && updateTimes.Contains(e.UpdateTime.Value));
            }
        }

        if (criteria.UpdateUsers.Count > 0)
        {
            query = query.Where(e => e.UpdateUser != null && criteria.UpdateUsers.Contains(e.UpdateUser));
        }

        return await query
            .OrderBy(e => e.Key1)
            .ThenBy(e => e.Key2)
            .ToListAsync(cancellationToken);
    }
}
