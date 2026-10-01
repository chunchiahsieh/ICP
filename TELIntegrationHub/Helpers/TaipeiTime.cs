using System.Globalization;

namespace TEL.IntegrationHub.Helpers;

public static class TaipeiTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime ToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeZone);
    }

    public static string FormatUtc(DateTime value, string format = "yyyy-MM-dd HH:mm:ss")
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone)
            .ToString(format, CultureInfo.InvariantCulture);
    }

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
