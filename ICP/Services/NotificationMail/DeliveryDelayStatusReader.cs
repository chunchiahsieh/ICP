using System.Data.Common;
using ICP.Data;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services.NotificationMail;

public static class DeliveryDelayStatusReader
{
    public static async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(
        ApplicationDbContext db, DateOnly date, IReadOnlyList<IcpHeader> headers,
        string frequency, CancellationToken cancellationToken)
    {
        var result = headers.ToDictionary(header => header.Id, _ => "Pending");
        if (headers.Count == 0) return result;

        var dayStart = new DateTimeOffset(date.Year, date.Month, date.Day,
            0, 0, 0, TimeSpan.FromHours(8)).UtcDateTime;
        var dayEnd = dayStart.AddDays(1);
        var dailyPrefix = $"delivery-delay:daily:{date:yyyyMMdd}:";
        try
        {
            var logs = await db.NotificationMailLogs.AsNoTracking()
                .Where(log => log.MailType == "DeliveryDelay"
                    && (log.EventKey.StartsWith(dailyPrefix)
                        || (log.CreatedUtc >= dayStart && log.CreatedUtc < dayEnd)))
                .Select(log => new { log.EventKey, log.State })
                .ToListAsync(cancellationToken);

            if (string.Equals(frequency, "Daily", StringComparison.OrdinalIgnoreCase))
            {
                var state = Resolve(logs.Where(log => log.EventKey.StartsWith(dailyPrefix,
                    StringComparison.Ordinal)).Select(log => log.State));
                foreach (var header in headers) result[header.Id] = state;
            }
            else if (string.Equals(frequency, "Immediate", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var header in headers)
                {
                    var prefix = $"delivery-delay:immediate:{header.Id:N}:";
                    result[header.Id] = Resolve(logs.Where(log => log.EventKey.StartsWith(prefix,
                        StringComparison.Ordinal)).Select(log => log.State));
                }
            }
        }
        catch (DbException)
        {
            foreach (var header in headers) result[header.Id] = "Unknown";
        }
        return result;
    }

    private static string Resolve(IEnumerable<string> states)
    {
        var values = states.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (values.Contains("Sent")) return "Sent";
        if (values.Contains("Sending")) return "Sending";
        if (values.Contains("Failed")) return "Failed";
        return "Pending";
    }
}
