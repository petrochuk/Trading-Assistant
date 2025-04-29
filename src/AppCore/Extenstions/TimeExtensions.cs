namespace AppCore.Extenstions;

public static class TimeExtensions
{
    public static TimeZoneInfo EasternStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    public static TimeZoneInfo CentralStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    public static TimeZoneInfo PacificStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    public static DateTimeOffset EstNow(this TimeProvider timeProvider) {
        var utcNow = timeProvider.GetLocalNow().UtcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(utcNow, EasternStandardTimeZone);
    }

    public static DateTime GetUtcNow() => DateTime.UtcNow;
}
