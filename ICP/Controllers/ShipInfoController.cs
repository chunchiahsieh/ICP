using ICP.Filters;
using ICP.Models;
using ICP.Models.ShipInfo;
using ICP.Services;

using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[ServiceFilter(typeof(ShipInfoApiExceptionFilter))]
public class ShipInfoController : Controller
{
    private readonly IShipInfoService _shipInfoService;

    public ShipInfoController(IShipInfoService shipInfoService)
    {
        _shipInfoService = shipInfoService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View("~/Views/FUNCTION/ShipInfo/View.cshtml");
    }

    [HttpGet]
    public IActionResult GetPageConfig()
    {
        var config = _shipInfoService.GetPageConfig();
        return Json(ApiResponse<ShipInfoPageConfig>.Ok(config));
    }

    [HttpGet]
    public async Task<IActionResult> GetLookupOptions(string category, CancellationToken cancellationToken = default)
    {
        var options = await _shipInfoService.GetLookupOptionsAsync(category, cancellationToken);
        return Json(ApiResponse<IReadOnlyList<ShipInfoLookupOption>>.Ok(options));
    }

    [HttpGet]
    public async Task<IActionResult> GetHeader(string headerKey, CancellationToken cancellationToken = default)
    {
        var data = await _shipInfoService.GetHeaderDataAsync(headerKey, cancellationToken);
        return Json(ApiResponse<Dictionary<string, object?>>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetDetail(string detailKey, CancellationToken cancellationToken = default)
    {
        var data = await _shipInfoService.GetDetailDataAsync(detailKey, cancellationToken);
        return Json(ApiResponse<Dictionary<string, object?>>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetDetailFilterOptions(
        string column,
        string headerKey,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _shipInfoService.GetDetailFilterOptionsAsync(column, headerKey, search, cancellationToken);
        return Json(options);
    }

    [HttpPost]
    public IActionResult ValidateHeader([FromBody] ShipInfoSaveRequest? request)
    {
        var values = request?.Values ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var errors = _shipInfoService.ValidateHeaderValues(values);
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(' ', errors)));
        }

        return Json(ApiResponse<object>.Ok(values));
    }

    [HttpPost]
    public IActionResult ValidateDetail([FromBody] ShipInfoSaveRequest? request)
    {
        var values = request?.Values ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var errors = _shipInfoService.ValidateDetailValues(values);
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(' ', errors)));
        }

        return Json(ApiResponse<object>.Ok(values));
    }

    [HttpPost]
    public async Task<IActionResult> SaveHeader(
        [FromBody] ShipInfoSaveRequest? request,
        CancellationToken cancellationToken = default)
    {
        var data = await _shipInfoService.SaveHeaderAsync(request ?? new ShipInfoSaveRequest(), User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<Dictionary<string, object?>>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> SaveDetail(
        [FromBody] ShipInfoSaveRequest? request,
        CancellationToken cancellationToken = default)
    {
        var data = await _shipInfoService.SaveDetailAsync(request ?? new ShipInfoSaveRequest(), User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<Dictionary<string, object?>>.Ok(data));
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QueryHeader(
        [FromForm] ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var model = await _shipInfoService.QueryHeaderTableAsync(criteria, cancellationToken);
        return PartialView("~/Views/FUNCTION/ShipInfo/View.HeaderList.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> GetHeaderFilterOptions(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _shipInfoService.GetHeaderFilterOptionsAsync(column, search, cancellationToken);
        return Json(options);
    }

    [HttpGet]

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QueryDetail(
        [FromForm] ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var model = await _shipInfoService.QueryDetailTableAsync(criteria, cancellationToken);
        return PartialView("~/Views/FUNCTION/ShipInfo/View.DetailList.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> SearchHeaders(
        [FromBody] ShipInfoSearchCriteria? criteria,
        CancellationToken cancellationToken = default)
    {
        var result = await _shipInfoService.SearchHeadersAsync(criteria ?? new ShipInfoSearchCriteria(), cancellationToken);
        return Json(ApiResponse<ShipInfoHeaderListResult>.Ok(result));
    }

    [HttpGet]
    public async Task<IActionResult> GetDetails(string headerKey, CancellationToken cancellationToken = default)
    {
        var result = await _shipInfoService.GetDetailsByHeaderKeyAsync(headerKey, cancellationToken);
        return Json(ApiResponse<ShipInfoDetailListResult>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteHeader(string headerKey, CancellationToken cancellationToken = default)
    {
        await _shipInfoService.DeleteHeaderAsync(headerKey, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { headerKey }));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDetail(string detailKey, CancellationToken cancellationToken = default)
    {
        await _shipInfoService.DeleteDetailAsync(detailKey, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { detailKey }));
    }

    [HttpGet]
    public async Task<IActionResult> GetCaseDrawerData(string headerKey, string caseType, CancellationToken cancellationToken = default)
    {
        var data = await _shipInfoService.GetCaseDrawerDataAsync(headerKey, caseType, cancellationToken);
        return Json(ApiResponse<ShipInfoCaseDrawerData>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeposit(string headerKey, CancellationToken cancellationToken = default)
    {
        var result = await _shipInfoService.CreateDepositCaseAsync(headerKey, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<ShipInfoCaseCreateResult>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateArur(string headerKey, CancellationToken cancellationToken = default)
    {
        var result = await _shipInfoService.CreateArurCaseAsync(headerKey, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<ShipInfoCaseCreateResult>.Ok(result));
    }
}
