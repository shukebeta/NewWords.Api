using Api.Framework.Extensions;

namespace Api.Framework.Helper;

public static class UnixTimestampHelper
{
    public static (long StartUnixTimestamp, long EndUnixTimestamp) GetDayUnixTimestamps(string localTimezone,
        DateTimeOffset? aDate = null)
    {
        DateTimeOffset date = aDate ?? DateTimeOffset.UtcNow;
        long startUnixTimestamp = date.GetDayStartTimestamp(TimeZoneInfo.FindSystemTimeZoneById(localTimezone));
        return (startUnixTimestamp, startUnixTimestamp + 86400 - 1);
    }
}
