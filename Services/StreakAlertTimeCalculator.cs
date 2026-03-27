using System.Collections.Concurrent;

namespace pqy_server.Services
{
    public static class StreakAlertTimeCalculator
    {
        public const string DefaultTimezone = "Asia/Kolkata";

        private static readonly ConcurrentDictionary<string, TimeZoneInfo> TimezoneCache = new(StringComparer.Ordinal);

        public static string NormalizeTimezone(string? timezone) =>
            string.IsNullOrWhiteSpace(timezone) ? DefaultTimezone : timezone.Trim();

        public static TimeZoneInfo ResolveTimeZone(string? timezone)
        {
            var effectiveTimezone = NormalizeTimezone(timezone);
            return TimezoneCache.GetOrAdd(effectiveTimezone, static key =>
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(key);
                }
                catch
                {
                    try
                    {
                        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
                    }
                    catch
                    {
                        return TimeZoneInfo.Utc;
                    }
                }
            });
        }

        public static DateTime ComputeNextFireUtc(string? timezone, int localHour, int localMinute, DateTime utcReference)
        {
            var zone = ResolveTimeZone(timezone);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcReference, zone);
            var candidateLocal = new DateTime(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                localHour,
                localMinute,
                0,
                DateTimeKind.Unspecified);

            if (candidateLocal <= DateTime.SpecifyKind(localNow, DateTimeKind.Unspecified))
                candidateLocal = candidateLocal.AddDays(1);

            return ConvertLocalToUtc(candidateLocal, zone);
        }

        public static DateOnly GetLocalDate(DateTime utcTime, string? timezone)
        {
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, ResolveTimeZone(timezone));
            return DateOnly.FromDateTime(localTime);
        }

        private static DateTime ConvertLocalToUtc(DateTime localTime, TimeZoneInfo zone)
        {
            if (zone.IsInvalidTime(localTime))
                localTime = localTime.AddHours(1);

            if (zone.IsAmbiguousTime(localTime))
            {
                var offsets = zone.GetAmbiguousTimeOffsets(localTime);
                var chosenOffset = offsets.Min();
                return new DateTimeOffset(localTime, chosenOffset).UtcDateTime;
            }

            return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
        }
    }
}
