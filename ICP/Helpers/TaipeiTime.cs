using System.Globalization;

namespace ICP.Helpers;

/// <summary>
/// Converts timestamps from UTC-backed integration tables to Taiwan local time.
/// Business tables that already use GETDATE()/DateTime.Now must not use this helper.
/// </summary>
public static class TaipeiTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime FromUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }

    public static string FormatUtc(DateTime value, string format = "yyyy-MM-dd HH:mm:ss")
        => FromUtc(value).ToString(format, CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Taipei Standard Time", "Asia/Taipei" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+08:00", TimeSpan.FromHours(8), "UTC+08:00", "UTC+08:00");
    }
}
