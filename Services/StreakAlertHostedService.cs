using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using System.Text.Json;

namespace pqy_server.Services
{
    /// <summary>
    /// Background service that fires FCM streak-alert notifications.
    ///
    /// Runs once per minute. For each active streak that has alerts defined,
    /// it converts the current UTC time to the user's local time (using their
    /// stored IANA timezone), then checks whether any alert hour:minute matches.
    /// When a match is found it sends an FCM push via FcmNotificationService.
    ///
    /// Soft-deleted streaks are excluded automatically via the HasQueryFilter on
    /// the Streak entity, so deleting a streak stops its alerts immediately.
    /// </summary>
    public class StreakAlertHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FcmNotificationService _fcm;
        private readonly ILogger<StreakAlertHostedService> _logger;

        public StreakAlertHostedService(
            IServiceScopeFactory scopeFactory,
            FcmNotificationService fcm,
            ILogger<StreakAlertHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _fcm = fcm;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StreakAlertHostedService started.");

            // Align to the next full minute before entering the loop
            await WaitForNextMinuteAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAlertsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "StreakAlertHostedService: unhandled error during alert processing.");
                }

                // Sleep until the next full minute
                await WaitForNextMinuteAsync(stoppingToken);
            }

            _logger.LogInformation("StreakAlertHostedService stopped.");
        }

        private async Task ProcessAlertsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var utcNow = DateTime.UtcNow;

            // Load all active streaks that have alerts, joined with the user's FCM token + timezone.
            // The HasQueryFilter on Streak already excludes IsDeleted = true rows.
            var rows = await db.Streaks
                .Where(s => s.Alerts != null && s.User.FcmToken != null)
                .Select(s => new
                {
                    s.StreakId,
                    s.Name,
                    s.Alerts,
                    s.User.FcmToken,
                    s.User.Timezone,
                })
                .ToListAsync(ct);

            _logger.LogInformation(
                "StreakAlertHostedService: tick at {UtcNow:HH:mm} UTC — {Count} streak(s) with alerts loaded.",
                utcNow, rows.Count);

            if (rows.Count == 0) return;

            var sent = 0;

            foreach (var row in rows)
            {
                // Resolve user local time. Defaults to Asia/Kolkata when timezone is not stored.
                var effectiveTz = string.IsNullOrWhiteSpace(row.Timezone) ? DefaultTimezone : row.Timezone;
                var localNow = ToLocalTime(utcNow, effectiveTz);
                var currentHour = localNow.Hour;
                var currentMinute = localNow.Minute;

                _logger.LogDebug(
                    "StreakAlertHostedService: streak {StreakId} ({Name}) — tz={Tz}, local={Local:HH:mm}, alerts={Alerts}",
                    row.StreakId, row.Name, effectiveTz, localNow, row.Alerts);

                // Parse alerts JSON  [{hour,minute,label?}]
                List<AlertEntry>? alertList = null;
                try
                {
                    alertList = JsonSerializer.Deserialize<List<AlertEntry>>(
                        row.Alerts!,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    _logger.LogWarning("StreakAlertHostedService: could not parse alerts for streak {StreakId}", row.StreakId);
                    continue;
                }

                if (alertList == null || alertList.Count == 0) continue;

                foreach (var alert in alertList)
                {
                    if (alert.Hour != currentHour || alert.Minute != currentMinute) continue;

                    var title = $"🔥 Time for {row.Name}!";
                    const string body = "Don't break the chain! Keep your streak burning.";

                    try
                    {
                        await _fcm.SendNotificationAsync(row.FcmToken!, title, body);
                        sent++;
                        _logger.LogInformation(
                            "StreakAlertHostedService: FCM sent for streak {StreakId} at {Hour}:{Minute:D2} (local: {Tz})",
                            row.StreakId, alert.Hour, alert.Minute, row.Timezone ?? "UTC");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "StreakAlertHostedService: FCM failed for streak {StreakId}", row.StreakId);
                    }

                    // Only one alert per streak per minute (break after first match)
                    break;
                }
            }

            if (sent > 0)
                _logger.LogInformation("StreakAlertHostedService: {Count} alert(s) sent at {UtcNow:HH:mm} UTC.", sent, utcNow);
        }

        /// <summary>Waits until the next full minute boundary (HH:mm:00).</summary>
        private static async Task WaitForNextMinuteAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var nextMinute = now.AddSeconds(60 - now.Second).AddMilliseconds(-now.Millisecond);
            var delay = nextMinute - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }

        /// <summary>
        /// Converts UTC time to the user's local time using an IANA timezone identifier.
        /// Falls back to UTC if the timezone is null, empty, or unrecognised.
        /// </summary>
        // Default timezone for users who haven't sent one yet — all users are currently in India.
        private const string DefaultTimezone = "Asia/Kolkata";

        private static DateTime ToLocalTime(DateTime utc, string? ianaTimezone)
        {
            var tz = string.IsNullOrWhiteSpace(ianaTimezone) ? DefaultTimezone : ianaTimezone;

            try
            {
                // TimeZoneInfo.FindSystemTimeZoneById supports IANA IDs natively on Linux (Railway).
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tzi);
            }
            catch
            {
                // Unrecognised timezone — fall back to IST rather than UTC
                var ist = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, ist);
            }
        }

        // ─── Local DTO (not exposed to clients) ──────────────────────────────────

        private class AlertEntry
        {
            public int Hour { get; set; }
            public int Minute { get; set; }
            public string? Label { get; set; }
        }
    }
}
