using ICP.Data;
using ICP.Infrastructure;
using ICP.Models;
using ICP.Services;
using ICP.Models.Ilc;
using ICP.Models.Icp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ICP.Controllers;

[PermissionModule]
public class UsersController : Controller
{
    private const int FilterOptionsLimit = 500;

    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "KeyId",
        "DepName",
        "UserName",
        "TelId",
        "EmailAddress",
        "DisplayName",
        "DepId",
        "DepName2",
        "CreateDate"
    };

    private readonly IlcDbContext _ilcDb;
    private readonly ApplicationDbContext _icpDb;
    private readonly UserAuthService _userAuthService;
    private readonly UserResourcePermissionService _userResourcePermissionService;

    public UsersController(
        IlcDbContext ilcDb,
        ApplicationDbContext icpDb,
        UserAuthService userAuthService,
        UserResourcePermissionService userResourcePermissionService)
    {
        _ilcDb = ilcDb;
        _icpDb = icpDb;
        _userAuthService = userAuthService;
        _userResourcePermissionService = userResourcePermissionService;
    }

    public IActionResult Index()
    {
        return View("View");
    }

    [HttpPost]
    public async Task<IActionResult> Query([FromForm] UsersSearchModel criteria, CancellationToken cancellationToken = default)
    {
        var list = await QueryUsersAsync(criteria, cancellationToken);
        return PartialView("View.List", new UsersSearchListViewModel { ListData = list });
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

    [HttpGet]
    public async Task<IActionResult> GetPermissions(int keyId, CancellationToken cancellationToken = default)
    {
        var user = await _ilcDb.UserInfoAd
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeyId == keyId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var response = await _userResourcePermissionService.BuildPermissionsResponseAsync(user, cancellationToken);

        return new JsonResult(response, UserPermissionsJsonOptions);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RefreshMySessionPermissions(CancellationToken cancellationToken = default)
    {
        var user = _userAuthService.GetSessionUserInfo();
        if (user is null || string.IsNullOrWhiteSpace(user.TelId))
        {
            return new JsonResult(new { success = false, message = "Unauthorized" })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (_userResourcePermissionService.IsSuperUserEnabled)
        {
            return Json(new { success = true, superUser = true, resourceCount = 0 });
        }

        await _userResourcePermissionService.RefreshSessionResourcesAsync(user, cancellationToken);
        var resourceCount = _userResourcePermissionService.GetSessionResources().Count;

        return Json(new { success = true, resourceCount });
    }

    private static readonly JsonSerializerOptions UserPermissionsJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _ilcDb.UserInfoAd.AsNoTracking();

        return column switch
        {
            "KeyId" => await DistinctKeyIdsAsync(query, search, cancellationToken),
            "DepName" => await DistinctNonEmptyAsync(query.Select(u => u.DepName), search, cancellationToken),
            "UserName" => await DistinctNonEmptyAsync(query.Select(u => u.UserName), search, cancellationToken),
            "TelId" => await DistinctNonEmptyAsync(query.Select(u => u.TelId), search, cancellationToken),
            "EmailAddress" => await DistinctNonEmptyAsync(query.Select(u => u.EmailAddress), search, cancellationToken),
            "DisplayName" => await DistinctNonEmptyAsync(query.Select(u => u.DisplayName), search, cancellationToken),
            "DepId" => await DistinctNonEmptyAsync(query.Select(u => u.DepId), search, cancellationToken),
            "DepName2" => await DistinctNonEmptyAsync(query.Select(u => u.DepName2), search, cancellationToken),
            "CreateDate" => await DistinctNonEmptyAsync(query.Select(u => u.CreateDate), search, cancellationToken),
            _ => []
        };
    }

    private static async Task<List<string>> DistinctKeyIdsAsync(
        IQueryable<UserInfoAd> query,
        string? search,
        CancellationToken cancellationToken)
    {
        var idQuery = query.Select(u => u.KeyId).Distinct();

        if (!string.IsNullOrWhiteSpace(search))
        {
            idQuery = idQuery.Where(id => id.ToString().Contains(search));
        }

        return await idQuery
            .OrderByDescending(id => id)
            .Take(FilterOptionsLimit)
            .Select(id => id.ToString())
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<string>> DistinctNonEmptyAsync(
        IQueryable<string?> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = selector.Where(v => v != null && v != "");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(v => v!.Contains(search));
        }

        return await query
            .Select(v => v!)
            .Distinct()
            .OrderBy(v => v)
            .Take(FilterOptionsLimit)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<UserInfoAd>> QueryUsersAsync(UsersSearchModel criteria, CancellationToken cancellationToken)
    {
        var query = _ilcDb.UserInfoAd.AsNoTracking();

        if (criteria.KeyIds.Count > 0)
        {
            var keyIds = criteria.KeyIds
                .Select(v => int.TryParse(v, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (keyIds.Count > 0)
            {
                query = query.Where(u => keyIds.Contains(u.KeyId));
            }
        }

        if (criteria.TelIds.Count > 0)
        {
            query = query.Where(u => u.TelId != null && criteria.TelIds.Contains(u.TelId));
        }

        if (criteria.UserNames.Count > 0)
        {
            query = query.Where(u => u.UserName != null && criteria.UserNames.Contains(u.UserName));
        }

        if (criteria.EmailAddresses.Count > 0)
        {
            query = query.Where(u => u.EmailAddress != null && criteria.EmailAddresses.Contains(u.EmailAddress));
        }

        if (criteria.DisplayNames.Count > 0)
        {
            query = query.Where(u => u.DisplayName != null && criteria.DisplayNames.Contains(u.DisplayName));
        }

        if (criteria.DepNames.Count > 0)
        {
            query = query.Where(u => u.DepName != null && criteria.DepNames.Contains(u.DepName));
        }

        if (criteria.DepIds.Count > 0)
        {
            query = query.Where(u => u.DepId != null && criteria.DepIds.Contains(u.DepId));
        }

        if (criteria.DepNames2.Count > 0)
        {
            query = query.Where(u => u.DepName2 != null && criteria.DepNames2.Contains(u.DepName2));
        }

        if (criteria.CreateDates.Count > 0)
        {
            query = query.Where(u => criteria.CreateDates.Contains(u.CreateDate));
        }

        return await query.OrderByDescending(u => u.KeyId).ToListAsync(cancellationToken);
    }
}
