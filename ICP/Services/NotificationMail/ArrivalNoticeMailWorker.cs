using System.Globalization;
using ICP.Data;
using ICP.Models.NotificationMail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services.NotificationMail;

/// <summary>Sends one mail per distinct Arrival Notice recipient set at configured daily slots.</summary>
public sealed class ArrivalNoticeMailWorker : BackgroundService
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<NotificationMailOptions> _options;
    private readonly ILogger<ArrivalNoticeMailWorker> _logger;

    public ArrivalNoticeMailWorker(IServiceScopeFactory scopeFactory,
        IOptionsMonitor<NotificationMailOptions> options, ILogger<ArrivalNoticeMailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Arrival notice mail polling failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var mail = options.ArrivalNotice;
        if (!mail.Enabled || !string.Equals(mail.Frequency, "Daily", StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(options.Smtp.Host) || string.IsNullOrWhiteSpace(options.Smtp.FromAddress))
            return;

        var now = DateTimeOffset.UtcNow.ToOffset(TaipeiOffset);
        var inSlot = mail.SendTimes.Any(text => TimeOnly.TryParseExact(text, "HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var slot)
            && slot.Hour == now.Hour && slot.Minute == now.Minute);
        if (!inSlot) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.ArrivalNoticeSchedules.Where(row => row.State == "Sending"
                && row.SendingUtc < DateTime.UtcNow.AddMinutes(-10))
            .ExecuteUpdateAsync(setters => setters
                // An interrupted send might already have reached SMTP; never send it twice.
                .SetProperty(row => row.State, "Failed")
                .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
        var snapshot = await ArrivalNoticeMailContent.LoadScheduledAsync(db, null,
            DateTime.UtcNow, cancellationToken);
        foreach (var group in snapshot.Groups)
        {
            var key = ArrivalNoticeMailContent.BuildEventKey(group);
            var claimTime = DateTime.UtcNow;
            if (!await DeliveryDelayMailWorker.TryClaimAsync(db, key, claimTime, cancellationToken)) continue;
            var scheduleIds = group.Schedules.Select(row => row.Id).ToList();
            var reserved = await db.ArrivalNoticeSchedules
                .Where(row => scheduleIds.Contains(row.Id) && row.State == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.State, "Sending")
                    .SetProperty(row => row.SendingUtc, claimTime), cancellationToken);
            if (reserved != scheduleIds.Count)
            {
                await db.ArrivalNoticeSchedules.Where(row => scheduleIds.Contains(row.Id)
                        && row.State == "Sending" && row.SendingUtc == claimTime)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, "Pending")
                        .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
                await db.NotificationMailDispatches.Where(d => d.EventKey == key && d.State == "Sending"
                        && d.UpdatedUtc == claimTime)
                    .ExecuteDeleteAsync(cancellationToken);
                continue;
            }
            var smtpAccepted = false;
            try
            {
                var subject = ArrivalNoticeMailContent.BuildSubject(mail.Title);
                var body = await ArrivalNoticeMailContent.BuildBodyAsync(db, group.Headers,
                    options.ShipInfoUrl, cancellationToken);
                await NotificationMailSender.SendAsync(db, options.Smtp, "ArrivalNotice", key,
                    group.MailTo, mail.CcTo.Where(address => !string.IsNullOrWhiteSpace(address)).ToList(),
                    subject, body, cancellationToken);
                smtpAccepted = true;

                await db.ArrivalNoticeSchedules.Where(row => scheduleIds.Contains(row.Id) && row.State == "Sending")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, "Sent")
                        .SetProperty(row => row.SentUtc, DateTime.UtcNow)
                        .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
                await db.NotificationMailDispatches.Where(d => d.EventKey == key)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.State, "Sent")
                        .SetProperty(d => d.UpdatedUtc, DateTime.UtcNow), cancellationToken);
                _logger.LogInformation("Arrival notice sent: {EventKey}, Invoices={Count}", key, group.Headers.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var terminalState = smtpAccepted ? "Sent" : "Failed";
                await db.ArrivalNoticeSchedules.Where(row => scheduleIds.Contains(row.Id)
                        && row.State == "Sending" && row.SendingUtc == claimTime)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, terminalState)
                        .SetProperty(row => row.SentUtc, smtpAccepted ? DateTime.UtcNow : (DateTime?)null)
                        .SetProperty(row => row.SendingUtc, (DateTime?)null), cancellationToken);
                await db.NotificationMailDispatches.Where(d => d.EventKey == key && d.State == "Sending")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.State, terminalState)
                        .SetProperty(d => d.UpdatedUtc, DateTime.UtcNow), cancellationToken);
                _logger.LogError(ex, "Arrival notice send failed: {EventKey}", key);
            }
        }
    }
}
