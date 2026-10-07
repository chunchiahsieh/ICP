using ICP.Data;
using ICP.Models.NotificationMail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services.NotificationMail;

public sealed class ControlledGoodsNoticeMailWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<NotificationMailOptions> options,
    ILogger<ControlledGoodsNoticeMailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Controlled goods notice polling failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var mail = current.ControlledGoodsArrival;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var todayTaipei = DateOnly.FromDateTime(DateTimeOffset.UtcNow
            .ToOffset(TimeSpan.FromHours(8)).Date);
        var todayText = todayTaipei.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var todayStartUtc = new DateTimeOffset(todayTaipei.Year, todayTaipei.Month,
            todayTaipei.Day, 0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
        // ETA is the send date. Never send an old pending notice on a later day.
        await db.ControlledGoodsNoticeSchedules.Where(row => row.State == "Pending"
                && row.ScheduledAtUtc < todayStartUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.State, "Failed"), cancellationToken);
        await db.ControlledGoodsNoticeSchedules.Where(row => row.State == "Sending"
                && row.SendingUtc < DateTime.UtcNow.AddMinutes(-10))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.State, "Failed")
                .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
        if (!mail.Enabled || !string.Equals(mail.Frequency, "Daily", StringComparison.OrdinalIgnoreCase)
            || mail.SendTimes.Count == 0) return;
        if (string.IsNullOrWhiteSpace(current.Smtp.Host)
            || string.IsNullOrWhiteSpace(current.Smtp.FromAddress)
            || !mail.MailTo.Any(address => !string.IsNullOrWhiteSpace(address))) return;
        var due = await db.ControlledGoodsNoticeSchedules.AsNoTracking()
            .Where(row => row.State == "Pending" && row.Eta == todayText
                && row.ScheduledAtUtc <= DateTime.UtcNow)
            .OrderBy(row => row.ScheduledAtUtc).ThenBy(row => row.InvoiceNo)
            .ToListAsync(cancellationToken);
        foreach (var schedule in due)
        {
            var claimTime = DateTime.UtcNow;
            var reserved = await db.ControlledGoodsNoticeSchedules
                .Where(row => row.Id == schedule.Id && row.State == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.State, "Sending")
                    .SetProperty(row => row.SendingUtc, claimTime), cancellationToken);
            if (reserved != 1) continue;
            var smtpAccepted = false;
            try
            {
                var header = await db.IcpHeaders.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.Id == schedule.HeaderId, cancellationToken);
                if (header is null) throw new InvalidOperationException("Ship Info header no longer exists.");
                if (!string.Equals(header.Eta?.Replace('/', '-'), schedule.Eta, StringComparison.Ordinal))
                    throw new InvalidOperationException("ETA changed after this notice was scheduled.");
                var eligible = await db.IcpDetails.AsNoTracking()
                    .AnyAsync(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                        && d.ElFlag != null && d.ElFlag.Trim() != "", cancellationToken);
                if (!eligible) throw new InvalidOperationException("EL Flag is no longer present.");
                var body = await ControlledGoodsNoticeContent.BuildBodyAsync(db, header,
                    current.ShipInfoUrl, cancellationToken);
                await NotificationMailSender.SendAsync(db, current.Smtp,
                    "ControlledGoodsArrival", $"controlled-goods:{schedule.Id:N}",
                    mail.MailTo.Where(address => !string.IsNullOrWhiteSpace(address)).ToList(),
                    mail.CcTo.Where(address => !string.IsNullOrWhiteSpace(address)).ToList(),
                    ControlledGoodsNoticeContent.BuildSubject(mail.Title, header.Eta), body, cancellationToken);
                smtpAccepted = true;
                await db.ControlledGoodsNoticeSchedules.Where(row => row.Id == schedule.Id && row.State == "Sending")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, "Sent")
                        .SetProperty(row => row.SentUtc, DateTime.UtcNow)
                        .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await db.ControlledGoodsNoticeSchedules.Where(row => row.Id == schedule.Id
                        && row.State == "Sending" && row.SendingUtc == claimTime)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, smtpAccepted ? "Sent" : "Failed")
                        .SetProperty(row => row.SentUtc, smtpAccepted ? DateTime.UtcNow : (DateTime?)null)
                        .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
                logger.LogError(ex, "Controlled goods notice send failed: {ScheduleId}", schedule.Id);
            }
        }
    }
}
