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
    public async Task SyncAfterSaveAsync(IcpHeader header, string? userName, CancellationToken ct)
    {
        var active = await db.ControlledGoodsNoticeSchedules
            .Where(row => row.HeaderId == header.Id && (row.State == "Pending" || row.State == "Sending"))
            .ToListAsync(ct);
        if (active.Any(row => row.State == "Sending"))
            throw new ShipInfoConcurrencyException("Controlled goods notice is being sent. Refresh and try again.");
        var eligible = string.Equals(header.ControlledGoodsNoticeFlag, "N", StringComparison.OrdinalIgnoreCase)
            && await db.IcpDetails.AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() == "2", ct);
        DateTime sendAt = default;
        var shouldSchedule = eligible && ControlledGoodsNoticeContent.TryScheduleUtc(header.Eta, DateTime.UtcNow,
            options.CurrentValue.ControlledGoodsArrival.SendTimes, out sendAt);
        foreach (var row in active)
        {
            if (shouldSchedule)
            {
                row.Eta = (header.Eta ?? "").Replace('/', '-');
                // Preserve an existing Pending slot on repeated saves.
            }
            else { row.State = "Cancelled"; row.CancelledUtc = DateTime.UtcNow; row.CancelledUser = userName; }
        }
        if (shouldSchedule && active.Count == 0)
            db.ControlledGoodsNoticeSchedules.Add(new ControlledGoodsNoticeSchedule
            {
                Id = Guid.NewGuid(), HeaderId = header.Id, InvoiceNo = header.InvoiceNo,
                Eta = (header.Eta ?? "").Replace('/', '-'), ScheduledAtUtc = sendAt,
                State = "Pending", CreatedUtc = DateTime.UtcNow, CreatedUser = userName
            });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ControlledGoodsNoticeEligibility> CheckEligibilityAsync(
        Guid headerId, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var header = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == headerId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        if (!ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
            return new(false, "StatusUnavailable");
        if (!string.Equals(header.ControlledGoodsNoticeFlag, "N", StringComparison.OrdinalIgnoreCase))
            return new(false, "AlreadySent");
        var eligible = await scope.ApplyDetails(db.IcpDetails).AsNoTracking()
            .AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() == "2", cancellationToken);
        if (!eligible)
            return new(false, "NoElFlag");
        if (!ControlledGoodsNoticeContent.TryScheduleUtc(header.Eta, DateTime.UtcNow,
                options.CurrentValue.ControlledGoodsArrival.SendTimes, out _))
            return new(false, "EtaUnavailable");
        var normalizedEta = (header.Eta ?? "").Replace('/', '-');
        var existing = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .AnyAsync(row => row.HeaderId == headerId
                && (row.State == "Pending" || row.State == "Sending"), cancellationToken);
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
        if (!string.Equals(header.ControlledGoodsNoticeFlag, "N", StringComparison.OrdinalIgnoreCase))
            throw new ShipInfoBusinessException("Controlled goods notice Flag must be N.");
        var eligible = await scope.ApplyDetails(db.IcpDetails).AsNoTracking()
            .AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() == "2", cancellationToken);
        if (!eligible)
            throw new ShipInfoBusinessException("At least one Ship Info Detail must have EL Flag 2.");
        if (!ControlledGoodsNoticeContent.TryScheduleUtc(header.Eta, DateTime.UtcNow,
                options.CurrentValue.ControlledGoodsArrival.SendTimes, out var sendAt))
            throw new ShipInfoBusinessException("Controlled goods notice send times are not configured.");
        var normalizedEta = (header.Eta ?? "").Replace('/', '-');
        if (await db.ControlledGoodsNoticeSchedules.AsNoTracking()
                .AnyAsync(row => row.HeaderId == headerId
                    && (row.State == "Pending" || row.State == "Sending"), cancellationToken))
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

    public async Task<Guid> CancelAsync(Guid scheduleId, string? userName, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        var schedule = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == scheduleId && row.State == "Pending", cancellationToken)
            ?? throw new ShipInfoNotFoundException("Pending controlled goods notice schedule not found.");
        _ = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken)
            ?? throw new ShipInfoNotFoundException("Ship Info header not found.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var changed = await db.ControlledGoodsNoticeSchedules
                .Where(row => row.Id == scheduleId && row.State == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.State, "Cancelled")
                    .SetProperty(row => row.CancelledUtc, DateTime.UtcNow)
                    .SetProperty(row => row.CancelledUser, userName), cancellationToken);
            if (changed != 1)
                throw new ShipInfoConcurrencyException("Controlled goods notice schedule is already being sent.");
            await db.IcpHeaders.Where(h => h.Id == schedule.HeaderId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(h => h.ControlledGoodsNoticeFlag, "Y")
                    .SetProperty(h => h.ControlledGoodsNoticeReason, "管制品到貨")
                    .SetProperty(h => h.UpdateTime, DateTime.Now)
                    .SetProperty(h => h.UpdateUser, userName), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return schedule.HeaderId;
    }

    public async Task<IReadOnlyList<ControlledGoodsNoticeScheduleRow>> ListAsync(
        string scheduleDate, CancellationToken cancellationToken)
    {
        EnsureEditPermission();
        if (!DateOnly.TryParseExact(scheduleDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ShipInfoBusinessException("Schedule date must use yyyy-MM-dd format.");
        var start = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
        var end = start.AddDays(1);
        var visibleHeaderIds = scope.Apply(db.IcpHeaders).Select(header => header.Id);
        var rows = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .Where(row => row.ScheduledAtUtc >= start && row.ScheduledAtUtc < end && row.State != "Cancelled"
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
