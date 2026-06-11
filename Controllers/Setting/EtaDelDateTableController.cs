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
public class EtaDelDateTableController : SystemConfigControllerBase
{
    private static readonly HashSet<string> AllowedLookupFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Destination Port",
        "EtaCalendarType",
        "WhCode",
        "FlightNo"
    };

    private readonly ApplicationDbContext _icpDb;

    public EtaDelDateTableController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
        _icpDb = icpDb;
    }

    protected override string Category => "EtaDelDateTable";

    [HttpGet]
    public async Task<IActionResult> GetFieldOptions(
        string field,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(field) || !AllowedLookupFields.Contains(field))
        {
            return BadRequest();
        }

        var query = _icpDb.SystemConfigs.AsNoTracking()
            .Where(e => e.Category == field && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.Key1.Contains(search) ||
                (e.Value1 != null && e.Value1.Contains(search)));
        }

        var items = await query
            .OrderBy(e => e.Key1)
            .Select(e => new { e.Key1, e.Value1 })
            .ToListAsync(cancellationToken);

        var options = items
            .Where(e => !string.IsNullOrWhiteSpace(e.Key1))
            .GroupBy(e => e.Key1.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    key = first.Key1.Trim(),
                    text = string.IsNullOrWhiteSpace(first.Value1) ? first.Key1.Trim() : first.Value1.Trim()
                };
            })
            .OrderBy(o => o.key)
            .Take(SearchFilterHelper.FilterOptionsLimit)
            .ToList();

        return Json(options);
    }

    protected override void ApplyEditModel(SystemConfig entity, SystemConfigEditModel model)
    {
        entity.Key1 = model.Key1!.Trim();
        entity.Key2 = string.Empty;
        entity.Value1 = model.Value1?.Trim();
        entity.Value2 = model.Value2?.Trim();
        entity.Value3 = model.Value3?.Trim();
        entity.Value4 = model.Value4?.Trim();
        entity.Value5 = model.Value5?.Trim();
        entity.Value6 = model.Value6?.Trim();
    }

    protected override SystemConfigEditModel MapToEditModel(SystemConfig entity)
    {
        return new SystemConfigEditModel
        {
            Id = entity.Id,
            Category = entity.Category,
            Key1 = entity.Key1,
            Value1 = entity.Value1,
            Value2 = entity.Value2,
            Value3 = entity.Value3,
            Value4 = entity.Value4,
            Value5 = entity.Value5,
            Value6 = entity.Value6
        };
    }

    protected override async Task<SystemConfigSearchListViewModel> BuildSearchListViewModelAsync(
        IList<SystemConfig> list,
        CancellationToken cancellationToken)
    {
        var etaCalendarTypeDisplay = await BuildEtaCalendarTypeDisplayMapAsync(list, cancellationToken);

        return new SystemConfigSearchListViewModel
        {
            ListData = list,
            PermissionPrefix = PermissionPrefix,
            EtaCalendarTypeDisplayByKey = etaCalendarTypeDisplay
        };
    }

    private async Task<IReadOnlyDictionary<string, string>> BuildEtaCalendarTypeDisplayMapAsync(
        IList<SystemConfig> list,
        CancellationToken cancellationToken)
    {
        var keys = list
            .Select(e => e.Value2)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var configs = await _icpDb.SystemConfigs.AsNoTracking()
            .Where(e => e.Category == "EtaCalendarType" && !e.IsDeleted && keys.Contains(e.Key1))
            .Select(e => new { e.Key1, e.Value1 })
            .ToListAsync(cancellationToken);

        return configs
            .Where(e => !string.IsNullOrWhiteSpace(e.Key1))
            .GroupBy(e => e.Key1.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var first = g.First();
                    return string.IsNullOrWhiteSpace(first.Value1) ? first.Key1.Trim() : first.Value1.Trim();
                },
                StringComparer.OrdinalIgnoreCase);
    }
}
