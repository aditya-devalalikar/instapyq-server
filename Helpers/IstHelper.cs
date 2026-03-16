namespace pqy_server.Helpers;

/// <summary>
/// IST is UTC+5:30 with no daylight saving time — offset is always fixed.
/// Use these helpers wherever a calendar date (today/week/month) is needed
/// so that leaderboards and streaks align with Indian midnight, not UTC midnight.
/// </summary>
public static class IstHelper
{
    /// <summary>Current date-time in Indian Standard Time (UTC+5:30).</summary>
    public static DateTime NowIst() => DateTime.UtcNow.AddHours(5).AddMinutes(30);

    /// <summary>Today's date in IST.</summary>
    public static DateOnly TodayIst() => DateOnly.FromDateTime(NowIst());
}
