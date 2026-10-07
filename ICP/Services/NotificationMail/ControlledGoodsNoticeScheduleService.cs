using System.Globalization;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.NotificationMail;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services.NotificationMail;

public sealed record ControlledGoodsNoticeScheduleRow(Guid Id, Guid HeaderId,
    string InvoiceNo, string Eta, DateTime ScheduledAtUtc, string State);
public sealed record ControlledGoodsNoticePreview(string MailTo, string CcTo,
    string Subject, string BodyHtml);
public sealed record ControlledGoodsNoticeEligibility(bool CanSchedule, string Reason);

public sealed class ControlledGoodsNoticeScheduleService(
    ApplicationDbContext db, PageDataScopeService scope,
    UserResourcePermissionService permissions, IOptionsMonitor<NotificationMailOptions> options)
{
    public async Task<ControlledGoodsNoticeEligibility> CheckEligibilityAsync(
        Guid headerId, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == headerId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
            return new(false, "StatusUnavailable");
        var eligible = await scope.ApplyDetails(db.IcpDetails).AsNoTracking()
            .AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() != "", cancellationToken);
        if (!eligible)
            return new(false, "NoElFlag");
        if (!ControlledGoodsNoticeContent.TryScheduleUtc(header.Eta, DateTime.UtcNow,
                options.CurrentValue.ControlledGoodsArrival.SendTimes, out _))
            return new(false, "EtaUnavailable");
        var normalizedEta = header.Eta!.Replace('/', '-');
        var existing = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .AnyAsync(row => row.HeaderId == headerId && row.Eta == normalizedEta
                && row.State != "Cancelled", cancellationToken);
        return existing
            ? new(false, "AlreadyScheduled")
            : new(true, string.Empty);
    }

    public async Task<ControlledGoodsNoticeScheduleRow> EnqueueAsync(Guid headerId,
        string? userName, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == headerId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
            throw new ShipInfoBusinessException("Ship Info header cannot be edited in its current status.");
        var eligible = await scope.ApplyDetails(db.IcpDetails).AsNoTracking()
            .AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() != "", cancellationToken);
        if (!eligible)
            throw new ShipInfoBusinessException("At least one Ship Info Detail must have a non-empty EL Flag.");
        if (!ControlledGoodsNoticeContent.TryScheduleUtc(header.Eta, DateTime.UtcNow,
                options.CurrentValue.ControlledGoodsArrival.SendTimes, out var sendAt))
            throw new ShipInfoBusinessException("ETA must be a future date with an available 10:00 Taipei send time.");
        var normalizedEta = header.Eta!.Replace('/', '-');
        if (await db.ControlledGoodsNoticeSchedules.AsNoTracking()
                .AnyAsync(row => row.HeaderId == headerId && row.Eta == normalizedEta
                    && row.State != "Cancelled", cancellationToken))
            throw new ShipInfoBusinessException("This Invoice is already scheduled for controlled goods arrival.");
        var schedule = new ControlledGoodsNoticeSchedule
        {
            Id = Guid.NewGuid(), HeaderId = header.Id, InvoiceNo = header.InvoiceNo,
            Eta = normalizedEta, ScheduledAtUtc = sendAt,
            State = "Pending", CreatedUtc = DateTime.UtcNow, CreatedUser = userName
        };
        db.ControlledGoodsNoticeSchedules.Add(schedule);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            throw new ShipInfoBusinessException("This Invoice is already scheduled for controlled goods arrival.");
        }
        return ToRow(schedule);
    }

    public async Task CancelAsync(Guid scheduleId, string? userName, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var schedule = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == scheduleId && row.State == "Pending", cancellationToken)
            ?? throw new ShipInfoNotFoundException("Pending controlled goods notice schedule not found.");
        _ = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        var changed = await db.ControlledGoodsNoticeSchedules
            .Where(row => row.Id == scheduleId && row.State == "Pending")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.State, "Cancelled")
                .SetProperty(row => row.CancelledUtc, DateTime.UtcNow)
                .SetProperty(row => row.CancelledUser, userName), cancellationToken);
        if (changed != 1)
            throw new ShipInfoConcurrencyException("Controlled goods notice schedule is already being sent.");
    }

    public async Task<IReadOnlyList<ControlledGoodsNoticeScheduleRow>> ListAsync(
        string eta, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        if (!DateOnly.TryParseExact(eta, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ShipInfoBusinessException("ETA must use yyyy-MM-dd format.");
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var visibleHeaderIds = scope.Apply(db.IcpHeaders).Select(header => header.Id);
        var rows = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .Where(row => row.Eta == dateText && row.State != "Cancelled"
                && visibleHeaderIds.Contains(row.HeaderId))
            .OrderBy(row => row.ScheduledAtUtc).ThenBy(row => row.InvoiceNo)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRow).ToList();
    }

    public async Task<ControlledGoodsNoticePreview> PreviewAsync(Guid scheduleId,
        CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var schedule = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == scheduleId && row.State != "Cancelled", cancellationToken)
            ?? throw new ShipInfoNotFoundException("Controlled goods notice schedule not found.");
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        var current = options.CurrentValue;
        var mail = current.ControlledGoodsArrival;
        return new ControlledGoodsNoticePreview(
            string.Join("; ", mail.MailTo), string.Join("; ", mail.CcTo),
            ControlledGoodsNoticeContent.BuildSubject(mail.Title, header.Eta),
            await ControlledGoodsNoticeContent.BuildBodyAsync(db, header,
                current.ShipInfoUrl, cancellationToken));
    }

    private void EnsureEditPermission()
    {
        if (!permissions.HasPermission(ShipInfoPermissionCodes.Edit))
            throw new ShipInfoForbiddenException("Ship Info Header edit permission is required.");
    }

    private static ControlledGoodsNoticeScheduleRow ToRow(ControlledGoodsNoticeSchedule row) =>
        new(row.Id, row.HeaderId, row.InvoiceNo, row.Eta,
            DateTime.SpecifyKind(row.ScheduledAtUtc, DateTimeKind.Utc), row.State);
}
