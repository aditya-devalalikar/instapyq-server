using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Models.Streak;
using System.Text.Json;

namespace pqy_server.Services
{
    public interface IStreakAlertScheduleService
    {
        Task SyncSchedulesForStreakAsync(Streak streak, CancellationToken ct = default);
        Task DeleteSchedulesForStreakAsync(int userId, int streakId, CancellationToken ct = default);
        Task ResyncSchedulesForUserAsync(int userId, CancellationToken ct = default);
        Task<int> BackfillMissingSchedulesAsync(CancellationToken ct = default);
    }

    public sealed class StreakAlertScheduleService : IStreakAlertScheduleService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly AppDbContext _context;
        private readonly ILogger<StreakAlertScheduleService> _logger;

        public StreakAlertScheduleService(
            AppDbContext context,
            ILogger<StreakAlertScheduleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SyncSchedulesForStreakAsync(Streak streak, CancellationToken ct = default)
        {
            var effectiveTimezone = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == streak.UserId)
                .Select(u => u.Timezone)
                .FirstOrDefaultAsync(ct);

            var normalizedAlerts = ParseAlerts(streak.Alerts);

            await DeleteSchedulesForStreakAsync(streak.UserId, streak.StreakId, ct);

            if (normalizedAlerts.Count == 0)
                return;

            var utcNow = DateTime.UtcNow;
            var timezone = StreakAlertTimeCalculator.NormalizeTimezone(effectiveTimezone);

            foreach (var alert in normalizedAlerts)
            {
                _context.StreakAlertSchedules.Add(new StreakAlertSchedule
                {
                    StreakId = streak.StreakId,
                    UserId = streak.UserId,
                    Timezone = timezone,
                    LocalHour = alert.Hour,
                    LocalMinute = alert.Minute,
                    Label = string.IsNullOrWhiteSpace(alert.Label) ? null : alert.Label.Trim(),
                    NextFireUtc = StreakAlertTimeCalculator.ComputeNextFireUtc(timezone, alert.Hour, alert.Minute, utcNow),
                    AttemptCount = 0,
                    IsActive = true,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }
        }

        public Task DeleteSchedulesForStreakAsync(int userId, int streakId, CancellationToken ct = default) =>
            _context.StreakAlertSchedules
                .Where(s => s.UserId == userId && s.StreakId == streakId)
                .ExecuteDeleteAsync(ct);

        public async Task ResyncSchedulesForUserAsync(int userId, CancellationToken ct = default)
        {
            var timezone = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.Timezone)
                .FirstOrDefaultAsync(ct);

            var effectiveTimezone = StreakAlertTimeCalculator.NormalizeTimezone(timezone);
            var utcNow = DateTime.UtcNow;

            var rows = await _context.StreakAlertSchedules
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                row.Timezone = effectiveTimezone;
                row.NextFireUtc = StreakAlertTimeCalculator.ComputeNextFireUtc(
                    effectiveTimezone,
                    row.LocalHour,
                    row.LocalMinute,
                    utcNow);
                row.LeaseUntilUtc = null;
                row.AttemptCount = 0;
                row.LastError = null;
                row.UpdatedAt = utcNow;
            }
        }

        public async Task<int> BackfillMissingSchedulesAsync(CancellationToken ct = default)
        {
            var streaks = await _context.Streaks
                .AsNoTracking()
                .Where(s => s.Alerts != null && s.Alerts != ""
                    && !_context.StreakAlertSchedules.Any(a => a.StreakId == s.StreakId))
                .OrderBy(s => s.StreakId)
                .ToListAsync(ct);

            var backfilled = 0;

            foreach (var streak in streaks)
            {
                try
                {
                    await SyncSchedulesForStreakAsync(streak, ct);
                    await _context.SaveChangesAsync(ct);
                    backfilled++;
                }
                catch (ArgumentException ex)
                {
                    _context.ChangeTracker.Clear();
                    _logger.LogWarning(ex,
                        "StreakAlertScheduleService: skipped backfill for streak {StreakId} because its alert payload is invalid.",
                        streak.StreakId);
                }
            }

            return backfilled;
        }

        private static List<AlertDto> ParseAlerts(string? alertsJson)
        {
            if (string.IsNullOrWhiteSpace(alertsJson))
                return [];

            List<AlertDto>? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<List<AlertDto>>(alertsJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Alert payload is not valid JSON.", ex);
            }

            if (parsed == null || parsed.Count == 0)
                return [];

            var deduped = new Dictionary<(int Hour, int Minute), AlertDto>();

            foreach (var alert in parsed)
            {
                if (alert.Hour is < 0 or > 23)
                    throw new ArgumentException("Alert hour must be between 0 and 23.");

                if (alert.Minute is < 0 or > 59)
                    throw new ArgumentException("Alert minute must be between 0 and 59.");

                var key = (alert.Hour, alert.Minute);
                if (!deduped.ContainsKey(key))
                {
                    deduped[key] = new AlertDto
                    {
                        Hour = alert.Hour,
                        Minute = alert.Minute,
                        Label = string.IsNullOrWhiteSpace(alert.Label) ? null : alert.Label.Trim()
                    };
                }
            }

            return deduped.Values
                .OrderBy(a => a.Hour)
                .ThenBy(a => a.Minute)
                .ToList();
        }
    }
}
