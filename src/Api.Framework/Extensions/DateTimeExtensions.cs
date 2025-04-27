namespace Api.Framework.Extensions;

public static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dateTime)
    {
        TimeSpan timeSpan = dateTime.ToUniversalTime() - DateTime.UnixEpoch;
        return (long) timeSpan.TotalSeconds;
    }

    public static bool EarlierThan(this DateTimeOffset dateTimeOffset, DateTimeOffset dateTimeOffset2)
    {
        return dateTimeOffset.CompareTo(dateTimeOffset2) < 0;
    }

    public static bool EarlierOrEqual(this DateTimeOffset dateTimeOffset, DateTimeOffset dateTimeOffset2)
    {
        return dateTimeOffset.CompareTo(dateTimeOffset2) <= 0;
    }

    public static bool LaterThan(this DateTimeOffset dateTimeOffset, DateTimeOffset dateTimeOffset2)
    {
        return dateTimeOffset.CompareTo(dateTimeOffset2) > 0;
    }

    public static long GetDayStartTimestamp(this DateTimeOffset dateTimeOffset, TimeZoneInfo timeZone)
    {
        var timestamp = dateTimeOffset.ToUnixTimeSeconds();
        var localDateTimeOffset = timestamp.ToDateTimeOffset(timeZone);
        return new DateTimeOffset(localDateTimeOffset.Year, localDateTimeOffset.Month, localDateTimeOffset.Day, 0, 0, 0,
            localDateTimeOffset.Offset).ToUnixTimeSeconds();
    }

    public static DateTimeOffset ToDateTimeOffset(this long timestamp, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(timestamp), timeZone);
    }

    public static DateTimeOffset ToDateTimeOffset(this long timestamp, string timeZoneId)
    {
        var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(timestamp), targetTimeZone);
    }

    public static string ToYmdHms(this DateTimeOffset dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
