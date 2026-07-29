using ICP.Models;
using ICP.Models.Report;
using ICP.Models.ShipInfo;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public abstract class ReportControllerBase : Controller
{
    private readonly IReportDataService _reportDataService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    protected ReportControllerBase(
        IReportDataService reportDataService,
        IStringLocalizer<SharedResource> localizer)
    {
        _reportDataService = reportDataService;
        _localizer = localizer;
    }

    protected abstract string ReportKey { get; }

    protected abstract string PermissionCode { get; }

    protected abstract string TitleKey { get; }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = _localizer[TitleKey].Value;
        ViewData["ReportKey"] = ReportKey;
        ViewData["PermissionCode"] = PermissionCode;
        ViewData["TitleKey"] = TitleKey;
        return View(ResolveViewPath());
    }

    [HttpGet]
    public IActionResult GetPageConfig() =>
        Json(ApiResponse<ShipInfoPageConfig>.Ok(_reportDataService.GetPageConfig(ReportKey)));

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QueryHeader(
        [FromForm] ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var model = await _reportDataService.QueryHeaderTableAsync(ReportKey, criteria, cancellationToken);
        return PartialView("~/Views/Report/Shared/View.HeaderList.cshtml", model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QueryDetail(
        [FromForm] ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var model = await _reportDataService.QueryDetailTableAsync(ReportKey, criteria, cancellationToken);
        return PartialView("~/Views/Report/Shared/View.DetailList.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> GetHeaderFilterOptions(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _reportDataService.GetHeaderFilterOptionsAsync(
            ReportKey,
            column,
            search,
            cancellationToken);
        return Json(options);
    }

    [HttpGet]
    public async Task<IActionResult> GetDetailFilterOptions(
        string column,
        string? headerKey = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = await _reportDataService.GetDetailFilterOptionsAsync(
            ReportKey,
            column,
            headerKey ?? string.Empty,
            search,
            cancellationToken);
        return Json(options);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DownloadExcel(
        [FromForm] ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var (content, fileName) = await _reportDataService.ExportExcelAsync(
            ReportKey,
            criteria,
            _localizer[TitleKey].Value,
            cancellationToken);

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private string ResolveViewPath() =>
        ReportKey switch
        {
            ReportKeys.ShippingReport => "~/Views/Report/ShippingReport/View.cshtml",
            ReportKeys.CompareIcpVsArUr => "~/Views/Report/CompareIcpVsArUr/View.cshtml",
            ReportKeys.MassDataReport => "~/Views/Report/MassDataReport/View.cshtml",
            _ => throw new InvalidOperationException($"Unknown report key: {ReportKey}")
        };
}
