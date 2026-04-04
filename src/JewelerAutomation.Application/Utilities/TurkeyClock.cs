namespace JewelerAutomation.Application.Utilities;

public static class TurkeyClock
{
    public static DateOnly TodayDateOnly()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(local.Date);
    }
}
