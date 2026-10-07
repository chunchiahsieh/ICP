using System.Globalization;
using ICP.Helpers;
using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.NotificationMail;
using ICP.Models.ShipInfo;
using ICP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services.NotificationMail;

public sealed record ArrivalNoticeScheduleRow(
    Guid Id, Guid HeaderId, string InvoiceNo, string MailTo, DateTime ScheduledAtUtc, string State);
public sealed record ArrivalNoticeSchedulePreview(string MailTo, string Subject, string BodyHtml);

public sealed class ArrivalNoticeScheduleService(
    ApplicationDbContext db,
    PageDataScopeService scope,
    UserResourcePermissionService permissions,
    IOptionsMonitor<NotificationMailOptions> options)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    public async Task<ArrivalNoticeScheduleRow> EnqueueAsync(
        Guid headerId, string? userName, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == headerId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
            throw new ShipInfoBusinessException("Ship Info header cannot be edited in its current status.");
        if (!ArrivalNoticeMailContent.TryParseMailTo(header.ArrivalNotice, out var mailTo, out var emailError))
            throw new ShipInfoBusinessException(emailError);

        var existing = await db.ArrivalNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.HeaderId == headerId && row.State == "Pending", cancellationToken);
        if (existing is not null)
            throw new ShipInfoBusinessException("This Invoice is already scheduled for Arrival Notice. Cancel the existing schedule before adding another.");

        var nextSlot = NextSlotUtc(options.CurrentValue.ArrivalNotice.SendTimes);
        var schedule = new ArrivalNoticeSchedule
        {
            Id = Guid.NewGuid(), HeaderId = header.Id, InvoiceNo = header.InvoiceNo,
            ArrivalNoticeValue = header.ArrivalNotice!.Trim(),
            RecipientKey = string.Join(";", mailTo).ToLowerInvariant(),
            ScheduledAtUtc = nextSlot,
            State = "Pending", CreatedUtc = DateTime.UtcNow, CreatedUser = userName
        };
        db.ArrivalNoticeSchedules.Add(schedule);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            throw new ShipInfoBusinessException("This Invoice is already scheduled for Arrival Notice.");
        }
        return ToRow(schedule);
    }

    public async Task CancelAsync(Guid scheduleId, string? userName, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var schedule = await db.ArrivalNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == scheduleId && row.State == "Pending", cancellationToken)
            ?? throw new ShipInfoNotFoundException("Pending Arrival Notice schedule not found.");
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        var cancelled = await db.ArrivalNoticeSchedules
            .Where(row => row.Id == scheduleId && row.State == "Pending")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.State, "Cancelled")
                .SetProperty(row => row.CancelledUtc, DateTime.UtcNow)
                .SetProperty(row => row.CancelledUser, userName), cancellationToken);
        if (cancelled != 1)
            throw new ShipInfoConcurrencyException("Arrival Notice schedule is already being sent. Refresh and try again.");
        // Arrival Notice remains unchanged, so the user can explicitly schedule it again.
    }

    public async Task<IReadOnlyList<ArrivalNoticeScheduleRow>> ListTodayAsync(
        Guid? currentHeaderId, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var now = DateTimeOffset.UtcNow.ToOffset(TaipeiOffset);
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TaipeiOffset).UtcDateTime;
        var end = start.AddDays(1);
        var visibleHeaderIds = scope.Apply(db.IcpHeaders).Select(header => header.Id);
        var rows = await db.ArrivalNoticeSchedules.AsNoTracking()
            .Where(row => ((row.ScheduledAtUtc >= start && row.ScheduledAtUtc < end)
                || (row.State == "Pending" && currentHeaderId.HasValue && row.HeaderId == currentHeaderId.Value)))
            .Where(row => row.State == "Pending" || row.State == "Sending"
                || row.State == "Sent" || row.State == "Failed")
            .Where(row => visibleHeaderIds.Contains(row.HeaderId))
            .OrderBy(row => row.ScheduledAtUtc).ThenBy(row => row.InvoiceNo)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRow).ToList();
    }

    public async Task<ArrivalNoticeSchedulePreview?> PreviewAsync(
        Guid headerId, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == headerId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ArrivalNoticeMailContent.TryParseMailTo(header.ArrivalNotice, out var addresses, out _))
            return null;
        var currentOptions = options.CurrentValue;
        return new ArrivalNoticeSchedulePreview(
            string.Join("; ", addresses),
            ArrivalNoticeMailContent.BuildSubject(currentOptions.ArrivalNotice.Title),
            await ArrivalNoticeMailContent.BuildBodyAsync(db, [header],
                currentOptions.ShipInfoUrl, cancellationToken));
    }

    public async Task<ArrivalNoticeSchedulePreview?> PreviewScheduleAsync(
        Guid scheduleId, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var schedule = await db.ArrivalNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == scheduleId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Arrival Notice schedule not found.");
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ArrivalNoticeMailContent.TryParseMailTo(schedule.ArrivalNoticeValue,
                out var addresses, out _))
            return null;
        var currentOptions = options.CurrentValue;
        return new ArrivalNoticeSchedulePreview(
            string.Join("; ", addresses),
            ArrivalNoticeMailContent.BuildSubject(currentOptions.ArrivalNotice.Title),
            await ArrivalNoticeMailContent.BuildBodyAsync(db, [header],
                currentOptions.ShipInfoUrl, cancellationToken));
    }

    private void EnsureEditPermission()
    {
        if (!permissions.HasPermission(ShipInfoPermissionCodes.Edit))
            throw new ShipInfoForbiddenException("Ship Info Header edit permission is required.");
    }

    private static ArrivalNoticeScheduleRow ToRow(ArrivalNoticeSchedule row) =>
        new(row.Id, row.HeaderId, row.InvoiceNo,
            string.Join("; ", ArrivalNoticeMailContent.ParseMailTo(row.ArrivalNoticeValue)),
            DateTime.SpecifyKind(row.ScheduledAtUtc, DateTimeKind.Utc), row.State);

    private static DateTime NextSlotUtc(IReadOnlyList<string> configuredTimes)
    {
        var slots = configuredTimes.Select(text => TimeOnly.TryParseExact(text, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : (TimeOnly?)null)
            .Where(value => value.HasValue).Select(value => value!.Value).Distinct().Order().ToList();
        if (slots.Count == 0) throw new ShipInfoBusinessException("Arrival Notice send times are not configured.");
        var now = DateTimeOffset.UtcNow.ToOffset(TaipeiOffset);
        foreach (var dayOffset in new[] { 0, 1 })
        {
            var date = now.Date.AddDays(dayOffset);
            foreach (var slot in slots)
            {
                var candidate = new DateTimeOffset(date.Year, date.Month, date.Day,
                    slot.Hour, slot.Minute, 0, TaipeiOffset);
                if (candidate > now) return candidate.UtcDateTime;
            }
        }
        throw new InvalidOperationException("Arrival Notice send time could not be resolved.");
    }
}
