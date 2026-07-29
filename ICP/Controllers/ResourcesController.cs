using ICP.Data;
using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ICP.Controllers;

[PermissionModule]
public class ResourcesController : Controller
{
    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ResourceCode",
        "ResourceName",
        "ResourceType",
        "SystemCode",
        "ModuleCode",
        "Route",
        "Sort",
        "IsVisible",
        "IsEnabled"
    };

    private readonly ApplicationDbContext _icpDb;

    public ResourcesController(ApplicationDbContext icpDb)
    {
        _icpDb = icpDb;
    }

    public IActionResult Index()
    {
        return View("View");
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(CancellationToken cancellationToken = default)
    {
        var list = await _icpDb.Resources
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Sort)
            .ThenBy(r => r.ResourceName)
            .Select(r => new { r.Id, r.ResourceCode, r.ResourceName })
            .ToListAsync(cancellationToken);

        return Json(list);
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] ResourcesSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryResourcesAsync(criteria, cancellationToken);
        return PartialView("View.List", new ResourcesSearchListViewModel { ListData = list });
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
        var query = _icpDb.Resources.AsNoTracking().Where(r => r.IsEnabled);

        return column switch
        {
            "ResourceCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceCode), search, cancellationToken),
            "ResourceName" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceName), search, cancellationToken),
            "ResourceType" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ResourceType), search, cancellationToken),
            "SystemCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.SystemCode), search, cancellationToken),
            "ModuleCode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.ModuleCode), search, cancellationToken),
            "Route" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(r => r.Route), search, cancellationToken),
            "Sort" => await SearchFilterHelper.DistinctIntAsync(query.Select(r => r.Sort), search, cancellationToken),
            "IsVisible" => await SearchFilterHelper.DistinctBoolAsync(query.Select(r => r.IsVisible), cancellationToken),
            "IsEnabled" => await SearchFilterHelper.DistinctBoolAsync(query.Select(r => r.IsEnabled), cancellationToken),
            _ => []
        };
    }

    private async Task<List<Resource>> QueryResourcesAsync(ResourcesSearchModel criteria, CancellationToken cancellationToken)
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

        if (criteria.Routes.Count > 0)
        {
            query = query.Where(r => r.Route != null && criteria.Routes.Contains(r.Route));
        }

        var sorts = SearchFilterHelper.ParseIntValues(criteria.Sorts);
        if (sorts.Count > 0)
        {
            query = query.Where(r => sorts.Contains(r.Sort));
        }

        var isVisibles = SearchFilterHelper.ParseBoolValues(criteria.IsVisibles);
        if (isVisibles.Count > 0)
        {
            query = query.Where(r => isVisibles.Contains(r.IsVisible));
        }

        var isEnableds = SearchFilterHelper.ParseBoolValues(criteria.IsEnableds);
        if (isEnableds.Count > 0)
        {
            query = query.Where(r => isEnableds.Contains(r.IsEnabled));
        }

        return await query
            .OrderBy(r => r.Sort)
            .ThenBy(r => r.ResourceName)
            .ToListAsync(cancellationToken);
    }
}
