using ICP.Models;
using ICP.Models.NotificationMail;
using ICP.Data;
using ICP.Services;
using ICP.Services.NotificationMail;
using System.Globalization;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Controllers;

public class NotificationMailController : Controller
{
    public const string PermissionCode = "Views.Shared._SidebarNav.Systems.NotificationMail";

    private readonly IOptionsMonitor<NotificationMailOptions> _options;
    private readonly UserResourcePermissionService _permissions;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ApplicationDbContext _db;

    public NotificationMailController(
        IOptionsMonitor<NotificationMailOptions> options,
        UserResourcePermissionService permissions,
        IStringLocalizer<SharedResource> localizer,
        ApplicationDbContext db)
    {
        _options = options;
        _permissions = permissions;
        _localizer = localizer;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? delayNotificationDate, string? arrivalDate, string? controlledEta,
        string? previewType, CancellationToken cancellationToken)
    {
        if (!_permissions.HasPermission(PermissionCode))
            return StatusCode(StatusCodes.Status403Forbidden, _localizer["Permission.AccessDenied"].Value);

        var options = _options.CurrentValue;
        var selectedDate = DateOnly.TryParseExact(delayNotificationDate, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate : DeliveryDelayMailWorker.TaipeiToday;
        var selectedDateDash = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var selectedEta = DateOnly.TryParseExact(controlledEta, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEta)
            ? parsedEta : DeliveryDelayMailWorker.TaipeiToday;
        var missing = new List<string>();
        if (!options.DeliveryDelay.Enabled) missing.Add(_localizer["NotificationMail.Enabled"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.Host)) missing.Add(_localizer["NotificationMail.Host"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.FromAddress)) missing.Add(_localizer["NotificationMail.From"].Value);
        if (!options.DeliveryDelay.MailTo.Any(address => !string.IsNullOrWhiteSpace(address)))
            missing.Add(_localizer["NotificationMail.MailTo"].Value);
        if (!string.Equals(options.DeliveryDelay.Frequency, "Daily", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.DeliveryDelay.Frequency, "Immediate", StringComparison.OrdinalIgnoreCase))
            missing.Add(_localizer["NotificationMail.Frequency"].Value);
        if (string.Equals(options.DeliveryDelay.Frequency, "Daily", StringComparison.OrdinalIgnoreCase)
            && !options.DeliveryDelay.SendTimes.Any(time =>
                TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            missing.Add(_localizer["NotificationMail.SendTimes"].Value);
        var arrivalMissing = new List<string>();
        if (!options.ArrivalNotice.Enabled) arrivalMissing.Add(_localizer["NotificationMail.Enabled"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.Host)) arrivalMissing.Add(_localizer["NotificationMail.Host"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.FromAddress)) arrivalMissing.Add(_localizer["NotificationMail.From"].Value);
        if (!string.Equals(options.ArrivalNotice.Frequency, "Daily", StringComparison.OrdinalIgnoreCase))
            arrivalMissing.Add(_localizer["NotificationMail.Frequency"].Value);
        else if (!options.ArrivalNotice.SendTimes.Any(time =>
                     TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            arrivalMissing.Add(_localizer["NotificationMail.SendTimes"].Value);
        var controlledMissing = new List<string>();
        if (!options.ControlledGoodsArrival.Enabled) controlledMissing.Add(_localizer["NotificationMail.Enabled"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.Host)) controlledMissing.Add(_localizer["NotificationMail.Host"].Value);
        if (string.IsNullOrWhiteSpace(options.Smtp.FromAddress)) controlledMissing.Add(_localizer["NotificationMail.From"].Value);
        if (!options.ControlledGoodsArrival.MailTo.Any(address => !string.IsNullOrWhiteSpace(address)))
            controlledMissing.Add(_localizer["NotificationMail.MailTo"].Value);
        if (!string.Equals(options.ControlledGoodsArrival.Frequency, "Daily", StringComparison.OrdinalIgnoreCase))
            controlledMissing.Add(_localizer["NotificationMail.Frequency"].Value);
        if (!options.ControlledGoodsArrival.SendTimes.Any(time => TimeOnly.TryParseExact(time,
                "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            controlledMissing.Add(_localizer["NotificationMail.SendTimes"].Value);
        var todayTaipei = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        var todayStartUtc = new DateTimeOffset(todayTaipei.Year, todayTaipei.Month,
            todayTaipei.Day, 0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
        ArrivalNoticeMailSnapshot arrivalSnapshot;
        var arrivalScheduleAvailable = true;
        try
        {
            arrivalSnapshot = await ArrivalNoticeMailContent.LoadScheduledAsync(_db,
                todayStartUtc, todayStartUtc.AddDays(1).AddTicks(-1), cancellationToken);
        }
        catch (DbException)
        {
            arrivalSnapshot = new ArrivalNoticeMailSnapshot([], []);
            arrivalScheduleAvailable = false;
            arrivalMissing.Add("ARRIVAL_NOTICE_SCHEDULE");
        }
        var controlledGoodsScheduleAvailable = true;
        try
        {
            await _db.ControlledGoodsNoticeSchedules.AsNoTracking().AnyAsync(cancellationToken);
        }
        catch (DbException)
        {
            controlledGoodsScheduleAvailable = false;
        }
        var model = new NotificationMailPageViewModel
        {
            Options = options,
            Today = DeliveryDelayMailWorker.TaipeiToday.ToString("yyyy/MM/dd"),
            SelectedNotificationDate = selectedDateDash,
            SelectedArrivalDate = DateOnly.TryParseExact(arrivalDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedArrivalDate)
                ? parsedArrivalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : DeliveryDelayMailWorker.TaipeiToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SelectedControlledEta = selectedEta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ActivePreviewType = previewType is "arrival" or "controlled" or "deposit"
                ? previewType : "delay",
            DeliveryDelayCount = 0,
            DeliveryDelayRows = [],
            DeliveryDelaySubject = string.Empty,
            DeliveryDelayBodyHtml = string.Empty,
            DeliveryDelayCanSend = missing.Count == 0,
            DeliveryDelayMissingSettings = missing,
            ArrivalNoticePreviews = [],
            ArrivalNoticeSchedules = [],
            ControlledGoodsNoticeRows = [],
            ControlledGoodsScheduleAvailable = controlledGoodsScheduleAvailable,
            ControlledGoodsMissingSettings = controlledMissing,
            ArrivalNoticeInvalidInvoiceNos = arrivalSnapshot.InvalidInvoiceNos,
            ArrivalNoticeMissingSettings = arrivalMissing,
            ArrivalScheduleAvailable = arrivalScheduleAvailable
        };
        return View("~/Views/Permission/NotificationMail/View.cshtml", model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> QueryList([FromForm] NotificationMailQueryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (!_permissions.HasPermission(PermissionCode)) return Forbid();
        var type = criteria.Type;
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);
        string TextFilter(string field) => criteria.Text.GetValueOrDefault(field)?.Trim() ?? string.Empty;
        bool DateFilter(string field, out DateOnly parsed) => DateOnly.TryParseExact(
            criteria.Date.GetValueOrDefault(field), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out parsed);
        var states = criteria.Checkbox.GetValueOrDefault("Status") ?? [];
        int count;
        IReadOnlyList<NotificationMailQueryRow> rows;
        if (type == "delay")
        {
            var query = _db.IcpHeaders.AsNoTracking().Where(header =>
                header.ReasonForDeliveryDelay != null && header.ReasonForDeliveryDelay.Trim() != ""
                && header.DelayNotificationDate != null && header.DelayNotificationDate != "");
            var invoice = TextFilter("InvoiceNo");
            var hawb = TextFilter("Hawb");
            var warehouse = TextFilter("Warehouse");
            var flt = TextFilter("Flt");
            var reason = TextFilter("Reason");
            if (invoice.Length > 0) query = query.Where(header => header.InvoiceNo.Contains(invoice));
            if (hawb.Length > 0) query = query.Where(header => header.Hawb != null && header.Hawb.Contains(hawb));
            if (warehouse.Length > 0) query = query.Where(header => header.Warehouse != null && header.Warehouse.Contains(warehouse));
            if (flt.Length > 0) query = query.Where(header => header.Flt != null && header.Flt.Contains(flt));
            if (reason.Length > 0) query = query.Where(header => header.ReasonForDeliveryDelay!.Contains(reason));
            if (DateFilter("ScheduledAt", out var notificationDate) || DateFilter("NotificationDate", out notificationDate))
            {
                var dash = notificationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var slash = notificationDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                query = query.Where(header => header.DelayNotificationDate == dash || header.DelayNotificationDate == slash);
            }
            if (DateFilter("Eta", out var eta))
            {
                var dash = eta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var slash = eta.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                query = query.Where(header => header.Eta == dash || header.Eta == slash);
            }
            var ordered = query.OrderBy(header => header.Hawb).ThenBy(header => header.InvoiceNo);
            var headers = states.Count > 0
                ? await ordered.ToListAsync(cancellationToken)
                : await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            var statuses = new Dictionary<Guid, string>();
            foreach (var group in headers.GroupBy(header => header.DelayNotificationDate))
            {
                if (DateOnly.TryParseExact(group.Key, ["yyyy-MM-dd", "yyyy/MM/dd"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var groupDate))
                {
                    var groupStatuses = await DeliveryDelayStatusReader.LoadAsync(_db, groupDate,
                        group.ToList(), _options.CurrentValue.DeliveryDelay.Frequency, cancellationToken);
                    foreach (var pair in groupStatuses) statuses[pair.Key] = pair.Value;
                }
                else foreach (var header in group) statuses[header.Id] = "Unknown";
            }
            if (states.Count > 0)
            {
                headers = headers.Where(header => states.Contains(statuses[header.Id])).ToList();
                count = headers.Count;
                headers = headers.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            else count = await query.CountAsync(cancellationToken);
            var failedLogs = await _db.NotificationMailLogs.AsNoTracking()
                .Where(log => log.MailType == "DeliveryDelay" && log.State == "Failed")
                .OrderByDescending(log => log.CreatedUtc).Take(1000)
                .Select(log => new { log.EventKey, log.ErrorMessage }).ToListAsync(cancellationToken);
            rows = headers.Select(header => new NotificationMailQueryRow(header.Id, header.InvoiceNo,
                header.Hawb, header.Warehouse, header.Flt, header.Eta,
                header.ReasonForDeliveryDelay, header.DelayNotificationDate, null, null,
                statuses[header.Id], statuses[header.Id] == "Failed"
                    ? failedLogs.FirstOrDefault(log => log.EventKey.StartsWith(
                        $"delivery-delay:daily:{header.DelayNotificationDate?.Replace("-", "").Replace("/", "")}:", StringComparison.Ordinal))?.ErrorMessage
                    : null)).ToList();
        }
        else if (type == "arrival")
        {
            var query = _db.ArrivalNoticeSchedules.AsNoTracking().Where(row =>
                row.State != "Cancelled");
            if (DateFilter("ScheduledAt", out var scheduledDate))
            {
                var start = new DateTimeOffset(scheduledDate.Year, scheduledDate.Month, scheduledDate.Day,
                    0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
                var end = start.AddDays(1);
                query = query.Where(row => row.ScheduledAtUtc >= start && row.ScheduledAtUtc < end);
            }
            var invoice = TextFilter("InvoiceNo");
            var mailTo = TextFilter("MailTo");
            if (invoice.Length > 0) query = query.Where(row => row.InvoiceNo.Contains(invoice));
            if (mailTo.Length > 0) query = query.Where(row => row.ArrivalNoticeValue.Contains(mailTo));
            if (states.Count > 0) query = query.Where(row => states.Contains(row.State));
            count = await query.CountAsync(cancellationToken);
            var schedules = await query.OrderBy(row => row.ScheduledAtUtc).ThenBy(row => row.InvoiceNo)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            var failed = schedules.Where(row => row.State == "Failed").ToList();
            var failedTimes = failed.Select(row => row.ScheduledAtUtc).Distinct().ToList();
            var failedRecipients = failed.Select(row => row.RecipientKey).Distinct().ToList();
            var siblingSchedules = await _db.ArrivalNoticeSchedules.AsNoTracking()
                .Where(row => row.State == "Failed" && failedTimes.Contains(row.ScheduledAtUtc)
                    && failedRecipients.Contains(row.RecipientKey))
                .ToListAsync(cancellationToken);
            var failedKeys = siblingSchedules.GroupBy(row => new { row.RecipientKey, row.ScheduledAtUtc })
                .ToDictionary(group => (group.Key.RecipientKey, group.Key.ScheduledAtUtc),
                    group => ArrivalNoticeMailContent.BuildEventKey(new ArrivalNoticeMailGroup(
                        group.ToList(), [], [], group.Key.RecipientKey, group.Key.ScheduledAtUtc)));
            var arrivalEventKeys = failedKeys.Values.ToList();
            var arrivalErrors = await _db.NotificationMailLogs.AsNoTracking()
                .Where(log => log.MailType == "ArrivalNotice" && log.State == "Failed"
                    && arrivalEventKeys.Contains(log.EventKey))
                .OrderByDescending(log => log.CreatedUtc)
                .Select(log => new { log.EventKey, log.ErrorMessage })
                .ToListAsync(cancellationToken);
            rows = schedules.Select(row => new NotificationMailQueryRow(row.Id, row.InvoiceNo,
                null, null, null, null, null, null,
                string.Join("; ", ArrivalNoticeMailContent.ParseMailTo(row.ArrivalNoticeValue)),
                row.ScheduledAtUtc, row.State,
                row.State == "Failed" && failedKeys.TryGetValue((row.RecipientKey, row.ScheduledAtUtc), out var key)
                    ? arrivalErrors.FirstOrDefault(log => log.EventKey == key)?.ErrorMessage : null)).ToList();
        }
        else if (type == "controlled")
        {
            var query = _db.ControlledGoodsNoticeSchedules.AsNoTracking()
                .Where(row => row.State != "Cancelled");
            if (DateFilter("ScheduledAt", out var scheduledDate))
            {
                var start = new DateTimeOffset(scheduledDate.Year, scheduledDate.Month, scheduledDate.Day,
                    0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
                var end = start.AddDays(1);
                query = query.Where(row => row.ScheduledAtUtc >= start && row.ScheduledAtUtc < end);
            }
            if (DateFilter("Eta", out var eta))
            {
                var day = eta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                query = query.Where(row => row.Eta == day);
            }
            var invoice = TextFilter("InvoiceNo");
            if (invoice.Length > 0) query = query.Where(row => row.InvoiceNo.Contains(invoice));
            if (states.Count > 0) query = query.Where(row => states.Contains(row.State));
            count = await query.CountAsync(cancellationToken);
            var schedules = await query.OrderBy(row => row.ScheduledAtUtc).ThenBy(row => row.InvoiceNo)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            var controlledKeys = schedules.Where(row => row.State == "Failed")
                .Select(row => $"controlled-goods:{row.Id:N}").ToList();
            var controlledErrors = await _db.NotificationMailLogs.AsNoTracking()
                .Where(log => log.MailType == "ControlledGoodsArrival" && log.State == "Failed"
                    && controlledKeys.Contains(log.EventKey))
                .OrderByDescending(log => log.CreatedUtc)
                .Select(log => new { log.EventKey, log.ErrorMessage })
                .ToListAsync(cancellationToken);
            rows = schedules.Select(row => new NotificationMailQueryRow(row.Id, row.InvoiceNo,
                null, null, null, row.Eta, null, null, null, row.ScheduledAtUtc, row.State)).ToList();
            rows = rows.Select(row => row with { FailureMessage = controlledErrors
                .FirstOrDefault(log => log.EventKey == $"controlled-goods:{row.Id:N}")?.ErrorMessage }).ToList();
        }
        else return BadRequest();
        Response.Headers["X-ICP-Total-Count"] = count.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-ICP-Page"] = page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-ICP-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);
        ViewBag.DelaySendTimes = string.Join("; ", _options.CurrentValue.DeliveryDelay.SendTimes);
        return PartialView("~/Views/Permission/NotificationMail/View.List.cshtml",
            new NotificationMailQueryViewModel { Type = type, Rows = rows });
    }

    [HttpGet]
    public IActionResult GetFilterOptions(string column)
    {
        if (!_permissions.HasPermission(PermissionCode)) return Forbid();
        if (column != "Status") return Json(Array.Empty<object>());
        return Json(new[] {
            new { value = "Pending", label = _localizer["NotificationMail.StatusNotSent"].Value },
            new { value = "Sending", label = _localizer["ShipInfo.ArrivalSchedule.Sending"].Value },
            new { value = "Sent", label = _localizer["ShipInfo.ArrivalSchedule.Sent"].Value },
            new { value = "Failed", label = _localizer["ShipInfo.ArrivalSchedule.Failed"].Value },
            new { value = "Unknown", label = _localizer["NotificationMail.StatusUnknown"].Value }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(string type, Guid? id, string? date,
        CancellationToken cancellationToken)
    {
        if (!_permissions.HasPermission(PermissionCode)) return Forbid();
        var options = _options.CurrentValue;
        if (type == "log")
        {
            if (!id.HasValue) return BadRequest();
            var log = await _db.NotificationMailLogs.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id.Value, cancellationToken);
            if (log is null) return NotFound();
            return Json(new { mailTo = log.MailTo, ccTo = log.CcTo,
                subject = log.Subject, bodyHtml = log.BodyHtml });
        }
        if (type == "delay")
        {
            var selected = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ? parsed : DeliveryDelayMailWorker.TaipeiToday;
            var day = selected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var slash = selected.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            var headers = await _db.IcpHeaders.AsNoTracking().Where(header =>
                (header.DelayNotificationDate == day || header.DelayNotificationDate == slash)
                && header.ReasonForDeliveryDelay != null && header.ReasonForDeliveryDelay.Trim() != "")
                .OrderBy(header => header.Hawb).ThenBy(header => header.InvoiceNo)
                .ToListAsync(cancellationToken);
            var invoiceNos = headers.Select(header => header.InvoiceNo).Distinct().ToList();
            var details = await _db.IcpDetails.AsNoTracking()
                .Where(detail => invoiceNos.Contains(detail.InvoiceNo))
                .OrderBy(detail => detail.InvoiceNo).ThenBy(detail => detail.InvoiceSeq)
                .ToListAsync(cancellationToken);
            return Json(new {
                mailTo = string.Join("; ", options.DeliveryDelay.MailTo),
                ccTo = string.Join("; ", options.DeliveryDelay.CcTo),
                subject = DeliveryDelayMailWorker.BuildSubject(options.DeliveryDelay.Title, headers, selected),
                bodyHtml = DeliveryDelayMailWorker.BuildBody(headers, details, options.ShipInfoUrl, selected)
            });
        }
        if (!id.HasValue) return BadRequest();
        if (type == "arrival")
        {
            var schedule = await _db.ArrivalNoticeSchedules.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id && row.State != "Cancelled", cancellationToken);
            if (schedule is null) return NotFound();
            var header = await _db.IcpHeaders.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == schedule.HeaderId, cancellationToken);
            if (header is null) return NotFound();
            return Json(new {
                mailTo = string.Join("; ", ArrivalNoticeMailContent.ParseMailTo(schedule.ArrivalNoticeValue)),
                ccTo = string.Join("; ", options.ArrivalNotice.CcTo),
                subject = ArrivalNoticeMailContent.BuildSubject(options.ArrivalNotice.Title),
                bodyHtml = await ArrivalNoticeMailContent.BuildBodyAsync(_db, [header],
                    options.ShipInfoUrl, cancellationToken)
            });
        }
        if (type == "controlled")
        {
            var schedule = await _db.ControlledGoodsNoticeSchedules.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id && row.State != "Cancelled", cancellationToken);
            if (schedule is null) return NotFound();
            var header = await _db.IcpHeaders.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == schedule.HeaderId, cancellationToken);
            if (header is null) return NotFound();
            return Json(new {
                mailTo = string.Join("; ", options.ControlledGoodsArrival.MailTo),
                ccTo = string.Join("; ", options.ControlledGoodsArrival.CcTo),
                subject = ControlledGoodsNoticeContent.BuildSubject(options.ControlledGoodsArrival.Title, header.Eta),
                bodyHtml = await ControlledGoodsNoticeContent.BuildBodyAsync(_db, header,
                    options.ShipInfoUrl, cancellationToken)
            });
        }
        return BadRequest();
    }
}
