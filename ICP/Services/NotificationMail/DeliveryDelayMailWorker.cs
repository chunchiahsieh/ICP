using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.NotificationMail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace ICP.Services.NotificationMail;

/// <summary>Polls Ship Info for today's delay-notification date. Other mail types remain disabled until defined.</summary>
public sealed class DeliveryDelayMailWorker : BackgroundService
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    public static DateOnly TaipeiToday => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TaipeiOffset).Date);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<NotificationMailOptions> _options;
    private readonly ILogger<DeliveryDelayMailWorker> _logger;

    public DeliveryDelayMailWorker(IServiceScopeFactory scopeFactory,
        IOptionsMonitor<NotificationMailOptions> options, ILogger<DeliveryDelayMailWorker> logger)
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
                _logger.LogError(ex, "Delivery delay mail polling failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var mail = options.DeliveryDelay;
        if (!mail.Enabled) return;
        if (string.IsNullOrWhiteSpace(options.Smtp.Host)
            || string.IsNullOrWhiteSpace(options.Smtp.FromAddress)
            || !mail.MailTo.Any(address => !string.IsNullOrWhiteSpace(address)))
        {
            _logger.LogWarning("Delivery delay mail enabled without SMTP host, sender or recipients; no mail sent.");
            return;
        }

        var now = DateTimeOffset.UtcNow.ToOffset(TaipeiOffset);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var headers = await GetTodayHeadersAsync(db, cancellationToken);
        if (headers.Count == 0) return;

        var frequency = mail.Frequency.Trim();
        if (frequency.Equals("Daily", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var text in mail.SendTimes.Distinct(StringComparer.Ordinal))
            {
                if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var slot))
                {
                    _logger.LogWarning("Invalid delivery delay send time: {SendTime}", text);
                    continue;
                }
                if (slot.Hour != now.Hour || slot.Minute != now.Minute) continue;
                var key = $"delivery-delay:daily:{now:yyyyMMdd}:{slot:HHmm}";
                await SendOnceAsync(db, key, headers, options, cancellationToken);
            }
        }
        else if (frequency.Equals("Immediate", StringComparison.OrdinalIgnoreCase))
        {
            // Existing records from earlier days are not treated as new immediate events.
            foreach (var header in headers.Where(h => (h.UpdateTime ?? h.CreateTime).Date == now.Date))
            {
                var revision = header.UpdateTime ?? header.CreateTime;
                var key = $"delivery-delay:immediate:{header.Id:N}:{revision.Ticks}";
                await SendOnceAsync(db, key, [header], options, cancellationToken);
            }
        }
        else _logger.LogWarning("Invalid delivery delay frequency: {Frequency}", frequency);
    }

    public static Task<List<IcpHeader>> GetTodayHeadersAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var today = TaipeiToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var todaySlash = TaipeiToday.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        return db.IcpHeaders.AsNoTracking()
            .Where(h => (h.DelayNotificationDate == today || h.DelayNotificationDate == todaySlash)
                && h.ReasonForDeliveryDelay != null
                && h.ReasonForDeliveryDelay.Trim() != "")
            .OrderBy(h => h.Hawb).ThenBy(h => h.InvoiceNo)
            .ToListAsync(cancellationToken);
    }

    private async Task SendOnceAsync(ApplicationDbContext db, string key,
        IReadOnlyList<IcpHeader> headers, NotificationMailOptions options, CancellationToken cancellationToken)
    {
        var claimTime = DateTime.UtcNow;
        if (!await TryClaimAsync(db, key, claimTime, cancellationToken)) return;
        var smtpAccepted = false;
        try
        {
            var invoiceNos = headers.Select(h => h.InvoiceNo).Distinct().ToList();
            var details = await db.IcpDetails.AsNoTracking()
                .Where(d => invoiceNos.Contains(d.InvoiceNo))
                .OrderBy(d => d.InvoiceNo).ThenBy(d => d.InvoiceSeq)
                .ToListAsync(cancellationToken);
            var subject = BuildSubject(options.DeliveryDelay.Title, headers);
            var body = BuildBody(headers, details, options.ShipInfoUrl);
            await NotificationMailSender.SendAsync(db, options.Smtp, "DeliveryDelay", key,
                options.DeliveryDelay.MailTo.Where(address => !string.IsNullOrWhiteSpace(address)).ToList(),
                options.DeliveryDelay.CcTo.Where(address => !string.IsNullOrWhiteSpace(address)).ToList(),
                subject, body, cancellationToken);
            smtpAccepted = true;
            await db.NotificationMailDispatches.Where(d => d.EventKey == key)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.State, "Sent")
                    .SetProperty(d => d.UpdatedUtc, DateTime.UtcNow), cancellationToken);
            _logger.LogInformation("Delivery delay mail sent: {EventKey}, Headers={Count}", key, headers.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed SMTP attempt may be retried on the next poll. A crash after SMTP success
            // but before marking Sent can still produce a duplicate after the stale-claim timeout.
            if (!smtpAccepted)
                await db.NotificationMailDispatches.Where(d => d.EventKey == key && d.State == "Sending"
                        && d.UpdatedUtc == claimTime)
                    .ExecuteDeleteAsync(cancellationToken);
            _logger.LogError(ex, "Delivery delay mail failed: {EventKey}", key);
        }
    }

    public static async Task<bool> TryClaimAsync(ApplicationDbContext db, string key,
        DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var claim = new NotificationMailDispatch
            {
                EventKey = key, State = "Sending", CreatedUtc = now, UpdatedUtc = now
            };
            db.NotificationMailDispatches.Add(claim);
            await db.SaveChangesAsync(cancellationToken);
            db.Entry(claim).State = EntityState.Detached;
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<NotificationMailDispatch>()
                         .Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            // Only reclaim a worker that has not progressed for ten minutes.
            return await db.NotificationMailDispatches
                .Where(d => d.EventKey == key && d.State == "Sending"
                    && d.UpdatedUtc < now.AddMinutes(-10))
                .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.UpdatedUtc, now), cancellationToken) == 1;
        }
    }

    public static string BuildSubject(string configuredTitle, IReadOnlyList<IcpHeader> headers,
        DateOnly? notificationDate = null)
    {
        var hawbs = string.Join(" / ", headers.Select(h => h.Hawb?.Trim()).Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var template = string.IsNullOrWhiteSpace(configuredTitle)
            ? "【C2/C3通關】{Date}-{Hawbs}" : configuredTitle;
        return template.Replace("{Date}", (notificationDate ?? TaipeiToday)
                .ToString("yyyy/MM/dd", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{Hawbs}", hawbs, StringComparison.Ordinal);
    }

    public static string BuildBody(IReadOnlyList<IcpHeader> headers, IReadOnlyList<IcpDetail> details,
        string shipInfoUrl, DateOnly? notificationDate = null)
    {
        static string E(object? value) => HtmlEncoder.Default.Encode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
        const string tableStyle = "border-collapse:collapse; font-family:Arial,sans-serif; font-size:14px; margin:0 0 16px 0;";
        const string headerStyle = "border:1px solid #000; background:#f2f2f2; padding:5px 7px; text-align:center; font-weight:bold;";
        const string cellStyle = "border:1px solid #000; padding:5px 7px; vertical-align:top;";
        static void AppendHeader(StringBuilder html, string text) => html.Append("<th style=\"").Append(headerStyle)
            .Append("\">").Append(E(text)).Append("</th>");
        static void AppendCell(StringBuilder html, object? value) => html.Append("<td style=\"").Append(cellStyle)
            .Append("\">").Append(E(value)).Append("</td>");
        var html = new StringBuilder("<div style=\"font-family:Arial,sans-serif; font-size:14px; color:#000;\">")
            .Append("<p style=\"margin:0;\">Dear All,</p>")
            .Append("<p style=\"margin:0;\">下述貨件因 C2/C3 通關，將延遲送達</p>");
        if (Uri.TryCreate(shipInfoUrl, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
        {
            var dateText = (notificationDate ?? TaipeiToday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var link = QueryHelpers.AddQueryString(url.AbsoluteUri, "DelayNotificationDate", dateText);
            html.Append("<p style=\"margin:0 0 4px 0;\"><a style=\"color:#b64c73;\" href=\"")
                .Append(E(link)).Append("\">Delivery Notification System</a></p>");
        }
        html.Append("<table style=\"").Append(tableStyle).Append("\"><thead><tr>");
        foreach (var label in new[] { "HAWB", "W/H", "FLT", "ETA", "到貨延遲原因", "Delivery Date" })
            AppendHeader(html, label);
        html.Append("</tr></thead><tbody>");
        foreach (var h in headers.OrderBy(h => h.Hawb, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(h => h.InvoiceNo, StringComparer.OrdinalIgnoreCase))
        {
            html.Append("<tr>");
            AppendCell(html, h.Hawb); AppendCell(html, h.Warehouse); AppendCell(html, h.Flt);
            AppendCell(html, h.Eta); AppendCell(html, h.ReasonForDeliveryDelay);
            AppendCell(html, h.DelayNotificationDate);
            html.Append("</tr>");
        }
        html.Append("</tbody></table><table style=\"").Append(tableStyle).Append("\"><thead><tr>");
        foreach (var label in new[] { "Invoice No", "Invoice Seq", "MAWB", "HAWB", "ETA", "PO", "W/H",
                     "NCDR No.", "Owner", "FLT", "Item No", "Description", "BU" })
            AppendHeader(html, label);
        html.Append("</tr></thead><tbody>");
        var detailRows = headers.SelectMany(h => details.Where(d => d.InvoiceNo == h.InvoiceNo),
                (h, d) => (Header: h, Detail: d))
            .OrderBy(row => row.Header.Hawb, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Header.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Detail.InvoiceSeq);
        foreach (var (h, d) in detailRows)
        {
            html.Append("<tr>");
            AppendCell(html, h.InvoiceNo); AppendCell(html, d.InvoiceSeq); AppendCell(html, h.Mawb);
            AppendCell(html, h.Hawb); AppendCell(html, h.Eta); AppendCell(html, d.TetPo);
            AppendCell(html, h.Warehouse); AppendCell(html, h.NcdrNo); AppendCell(html, h.Owner);
            AppendCell(html, h.Flt); AppendCell(html, d.ItemNo); AppendCell(html, d.Description);
            AppendCell(html, h.Bu);
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

}
