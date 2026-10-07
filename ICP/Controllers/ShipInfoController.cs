using ICP.Filters;
using ICP.Models;
using ICP.Models.ShipInfo;
using ICP.Repositories;
using ICP.Services;
using ICP.Services.NotificationMail;

using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[ServiceFilter(typeof(ShipInfoApiExceptionFilter))]
public class ShipInfoController : Controller
{
    private readonly IShipInfoService _shipInfoService;
    private readonly IShipInfoRepository _repository;
    private readonly ShipInfoAttachmentService _attachments;
    private readonly ArrivalNoticeScheduleService _arrivalNoticeSchedules;
    private readonly DeliveryDelayPreviewService _deliveryDelayPreview;
    private readonly ControlledGoodsNoticeScheduleService _controlledGoodsSchedules;

    public ShipInfoController(IShipInfoService shipInfoService, IShipInfoRepository repository,
        ShipInfoAttachmentService attachments, ArrivalNoticeScheduleService arrivalNoticeSchedules,
        DeliveryDelayPreviewService deliveryDelayPreview,
        ControlledGoodsNoticeScheduleService controlledGoodsSchedules)
    {
        _shipInfoService = shipInfoService;
        _repository = repository;
        _attachments = attachments;
        _arrivalNoticeSchedules = arrivalNoticeSchedules;
        _deliveryDelayPreview = deliveryDelayPreview;
        _controlledGoodsSchedules = controlledGoodsSchedules;
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
    public async Task<IActionResult> DiscardHeader(
        [FromBody] ShipInfoDiscardRequest? request,
        CancellationToken cancellationToken = default)
    {
        await _shipInfoService.DiscardHeaderAsync(request?.HeaderKey ?? string.Empty, request?.Reason, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { headerKey = request?.HeaderKey }));
    }

    [HttpGet]
    public async Task<IActionResult> GetTodayArrivalNoticeSchedules(
        Guid? currentHeaderId, CancellationToken cancellationToken = default)
    {
        var data = await _arrivalNoticeSchedules.ListTodayAsync(currentHeaderId, cancellationToken);
        return Json(ApiResponse<IReadOnlyList<ArrivalNoticeScheduleRow>>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetDeliveryDelayPreview(
        string delayNotificationDate, CancellationToken cancellationToken = default)
    {
        var data = await _deliveryDelayPreview.GetByDelayNotificationDateAsync(delayNotificationDate, cancellationToken);
        return Json(ApiResponse<DeliveryDelayTodayPreview>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetControlledGoodsNoticeSchedules(
        string eta, CancellationToken cancellationToken = default)
    {
        var data = await _controlledGoodsSchedules.ListAsync(eta, cancellationToken);
        return Json(ApiResponse<IReadOnlyList<ControlledGoodsNoticeScheduleRow>>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetControlledGoodsNoticeEligibility(
        Guid headerId, CancellationToken cancellationToken = default)
    {
        var data = await _controlledGoodsSchedules.CheckEligibilityAsync(headerId, cancellationToken);
        return Json(ApiResponse<ControlledGoodsNoticeEligibility>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetControlledGoodsNoticePreview(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var data = await _controlledGoodsSchedules.PreviewAsync(scheduleId, cancellationToken);
        return Json(ApiResponse<ControlledGoodsNoticePreview>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> ScheduleControlledGoodsNotice(
        Guid headerId, CancellationToken cancellationToken = default)
    {
        var data = await _controlledGoodsSchedules.EnqueueAsync(headerId,
            User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<ControlledGoodsNoticeScheduleRow>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> CancelControlledGoodsNoticeSchedule(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        await _controlledGoodsSchedules.CancelAsync(scheduleId,
            User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { scheduleId }));
    }

    [HttpGet]
    public async Task<IActionResult> GetArrivalNoticeSchedulePreview(
        Guid headerId, CancellationToken cancellationToken = default)
    {
        var data = await _arrivalNoticeSchedules.PreviewAsync(headerId, cancellationToken);
        return Json(ApiResponse<ArrivalNoticeSchedulePreview?>.Ok(data));
    }

    [HttpGet]
    public async Task<IActionResult> GetArrivalNoticeQueuedPreview(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var data = await _arrivalNoticeSchedules.PreviewScheduleAsync(scheduleId, cancellationToken);
        return Json(ApiResponse<ArrivalNoticeSchedulePreview?>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> ScheduleArrivalNotice(Guid headerId, CancellationToken cancellationToken = default)
    {
        var data = await _arrivalNoticeSchedules.EnqueueAsync(headerId, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<ArrivalNoticeScheduleRow>.Ok(data));
    }

    [HttpPost]
    public async Task<IActionResult> CancelArrivalNoticeSchedule(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        await _arrivalNoticeSchedules.CancelAsync(scheduleId, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { scheduleId }));
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

    [HttpGet]
    public async Task<IActionResult> GetAttachments([FromQuery(Name = "headerKey")] string headerId, CancellationToken cancellationToken = default)
    {
        var header = await RequireHeaderAsync(headerId, cancellationToken);
        return Json(ApiResponse<IReadOnlyList<ShipInfoAttachmentDto>>.Ok(await _attachments.ListAsync(header.Id, cancellationToken)));
    }

    [HttpPost]
    public async Task<IActionResult> UploadAttachment([FromQuery(Name = "headerKey")] string headerId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var header = await RequireHeaderAsync(headerId, cancellationToken);
        var item = await _attachments.UploadAsync(header.Id, file, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<ShipInfoAttachmentDto>.Ok(item, "Uploaded"));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment([FromQuery(Name = "headerKey")] string headerId, Guid id, CancellationToken cancellationToken = default)
    {
        var header = await RequireHeaderAsync(headerId, cancellationToken);
        var (item, path) = await _attachments.RequireActiveAsync(header.Id, id, cancellationToken);
        if (!System.IO.File.Exists(path)) return NotFound(ApiResponse<object>.Fail($"Attachment file is missing: {item.OriginalFileName}"));
        return PhysicalFile(path, item.ContentType ?? "application/octet-stream", item.OriginalFileName);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAttachment([FromQuery(Name = "headerKey")] string headerId, Guid id, CancellationToken cancellationToken = default)
    {
        var header = await RequireHeaderAsync(headerId, cancellationToken);
        await _attachments.DeleteAsync(header.Id, id, User.Identity?.Name, cancellationToken);
        return Json(ApiResponse<object>.Ok(new { id }));
    }

    private async Task<ICP.Models.Icp.IcpHeader> RequireHeaderAsync(string headerId, CancellationToken ct)
    {
        if (!Guid.TryParse(headerId, out var id))
        {
            throw new ArgumentException("Header ID is invalid.", nameof(headerId));
        }

        return await _repository.GetHeaderByIdAsync(id, ct) ?? throw new KeyNotFoundException("Header not found.");
    }
}
