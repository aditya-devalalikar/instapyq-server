using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using pqy_server.Data;
using pqy_server.Models.Streak;

namespace pqy_server.Services
{
    public interface IStreakService
    {
        // Streaks CRUD
        Task<List<StreakDto>> GetStreaksAsync(int userId);
        Task<StreakDto> CreateStreakAsync(int userId, CreateStreakRequest req);
        Task<StreakDto> UpdateStreakAsync(int userId, int streakId, UpdateStreakRequest req);
        Task DeleteStreakAsync(int userId, int streakId);

        // Progress
        Task<StreakMonthlyProgressDto> ToggleProgressAsync(int userId, int streakId, string dateStr);
        Task<List<StreakMonthlyProgressDto>> GetProgressAsync(int userId, int streakId, string fromMonth, string toMonth);

        // Study sessions
        Task UpsertAggregatesAsync(int userId, List<DailyAggregateRequest> aggregates, Dictionary<string, int> clientToServerIdMap);
        Task<List<DailySummaryDto>> GetSummaryAsync(int userId, DateOnly from, DateOnly to);

        // Full first-time sync
        Task<Dictionary<string, int>> FullSyncAsync(int userId, FullSyncRequest req);
    }

    public class StreakService : IStreakService
    {
        private readonly AppDbContext _context;

        // App is India-only — all "today" comparisons use IST (UTC+5:30)
        private static readonly TimeZoneInfo IstZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        private static DateOnly TodayIst() =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone));

        public StreakService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static StreakDto ToDto(Models.Streak.Streak s) => new()
        {
            StreakId = s.StreakId,
            ClientId = s.ClientId,
            Name = s.Name,
            Description = s.Description,
            Color = s.Color,
            Icon = s.Icon,
            Frequency = s.Frequency,
            SpecificDays = s.SpecificDays != null
                ? JsonSerializer.Deserialize<List<int>>(s.SpecificDays)
                : null,
            Category = s.Category,
            IsTimer = s.IsTimer,
            TargetMinutes = s.TargetMinutes,
            Alerts = s.Alerts != null
                ? JsonSerializer.Deserialize<List<AlertDto>>(s.Alerts)
                : null,
            CurrentStreakDays = s.CurrentStreakDays,
            LongestStreakDays = s.LongestStreakDays,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
        };

        /// <summary>
        /// Recalculates and persists CurrentStreakDays and LongestStreakDays
        /// by decoding bitmasks across all months for a streak.
        /// </summary>
        private async Task RecalcStreakCacheAsync(int streakId)
        {
            var allProgress = await _context.StreakMonthlyProgress
                .Where(p => p.StreakId == streakId)
                .OrderBy(p => p.YearMonth)
                .ToListAsync();

            // Expand bitmasks into a sorted set of completed dates
            var completedDates = new SortedSet<DateOnly>();
            foreach (var mp in allProgress)
            {
                if (!DateOnly.TryParseExact(mp.YearMonth + "-01", "yyyy-MM-dd", out var firstDay))
                    continue;

                var daysInMonth = DateTime.DaysInMonth(firstDay.Year, firstDay.Month);
                for (int d = 1; d <= daysInMonth; d++)
                {
                    if ((mp.DaysMask & (1 << (d - 1))) != 0)
                        completedDates.Add(new DateOnly(firstDay.Year, firstDay.Month, d));
                }
            }

            if (completedDates.Count == 0)
            {
                await _context.Streaks
                    .Where(s => s.StreakId == streakId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.CurrentStreakDays, 0)
                        .SetProperty(x => x.LongestStreakDays, 0)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
                return;
            }

            // Calculate longest and current streaks
            int longest = 1, current = 1;
            var dates = completedDates.ToList();
            for (int i = 1; i < dates.Count; i++)
            {
                if (dates[i] == dates[i - 1].AddDays(1))
                {
                    current++;
                    if (current > longest) longest = current;
                }
                else
                {
                    current = 1;
                }
            }

            // Current streak: only valid if the last completed date is today or yesterday (IST)
            var today = TodayIst();
            var lastDate = dates[^1];
            int currentStreak = (lastDate == today || lastDate == today.AddDays(-1)) ? current : 0;

            await _context.Streaks
                .Where(s => s.StreakId == streakId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.CurrentStreakDays, currentStreak)
                    .SetProperty(x => x.LongestStreakDays, longest)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        }

        // ─── Streaks CRUD ─────────────────────────────────────────────────────────

        public async Task<List<StreakDto>> GetStreaksAsync(int userId)
        {
            var streaks = await _context.Streaks
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            return streaks.Select(ToDto).ToList();
        }

        public async Task<StreakDto> CreateStreakAsync(int userId, CreateStreakRequest req)
        {
            // Idempotent: if ClientId already exists for this user, return existing
            var existing = await _context.Streaks
                .FirstOrDefaultAsync(s => s.ClientId == req.ClientId && s.UserId == userId);

            if (existing != null)
                return ToDto(existing);

            var streak = new Models.Streak.Streak
            {
                UserId = userId,
                ClientId = req.ClientId,
                Name = req.Name,
                Description = req.Description,
                Color = req.Color,
                Icon = req.Icon,
                Frequency = req.Frequency,
                SpecificDays = req.SpecificDays != null
                    ? JsonSerializer.Serialize(req.SpecificDays)
                    : null,
                Category = req.Category,
                IsTimer = req.IsTimer,
                TargetMinutes = req.TargetMinutes,
                Alerts = req.Alerts != null
                    ? JsonSerializer.Serialize(req.Alerts)
                    : null,
            };

            _context.Streaks.Add(streak);
            await _context.SaveChangesAsync();
            return ToDto(streak);
        }

        public async Task<StreakDto> UpdateStreakAsync(int userId, int streakId, UpdateStreakRequest req)
        {
            var streak = await _context.Streaks
                .FirstOrDefaultAsync(s => s.StreakId == streakId && s.UserId == userId)
                ?? throw new KeyNotFoundException("Streak not found.");

            if (req.Name != null) streak.Name = req.Name;
            if (req.Description != null) streak.Description = req.Description;
            if (req.Color != null) streak.Color = req.Color;
            if (req.Icon != null) streak.Icon = req.Icon;
            if (req.Frequency != null) streak.Frequency = req.Frequency;
            if (req.SpecificDays != null)
                streak.SpecificDays = JsonSerializer.Serialize(req.SpecificDays);
            if (req.Category != null) streak.Category = req.Category;
            if (req.IsTimer.HasValue) streak.IsTimer = req.IsTimer.Value;
            if (req.TargetMinutes.HasValue) streak.TargetMinutes = req.TargetMinutes;
            if (req.Alerts != null)
                streak.Alerts = JsonSerializer.Serialize(req.Alerts);

            streak.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ToDto(streak);
        }

        public async Task DeleteStreakAsync(int userId, int streakId)
        {
            // Delete all monthly progress bitmasks for this streak
            await _context.StreakMonthlyProgress
                .Where(p => p.StreakId == streakId && p.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Streaks
                .Where(s => s.StreakId == streakId && s.UserId == userId)
                .ExecuteDeleteAsync();
        }

        // ─── Progress ─────────────────────────────────────────────────────────────

        public async Task<StreakMonthlyProgressDto> ToggleProgressAsync(int userId, int streakId, string dateStr)
        {
            if (!DateOnly.TryParseExact(dateStr, "yyyy-MM-dd", out var date))
                throw new ArgumentException("Invalid date format. Use YYYY-MM-DD.");

            // Verify streak belongs to user
            var streakExists = await _context.Streaks
                .AnyAsync(s => s.StreakId == streakId && s.UserId == userId);
            if (!streakExists)
                throw new KeyNotFoundException("Streak not found.");

            var yearMonth = date.ToString("yyyy-MM");
            var dayBit = 1 << (date.Day - 1);

            var progress = await _context.StreakMonthlyProgress
                .FirstOrDefaultAsync(p => p.StreakId == streakId && p.YearMonth == yearMonth);

            if (progress == null)
            {
                progress = new StreakMonthlyProgress
                {
                    StreakId = streakId,
                    UserId = userId,
                    YearMonth = yearMonth,
                    DaysMask = dayBit,
                };
                _context.StreakMonthlyProgress.Add(progress);
            }
            else
            {
                // Toggle: if bit is set → clear it; if not set → set it
                progress.DaysMask = (progress.DaysMask & dayBit) != 0
                    ? progress.DaysMask & ~dayBit
                    : progress.DaysMask | dayBit;
            }

            await _context.SaveChangesAsync();

            // Recalc is best-effort: progress is already saved; cache self-heals on next toggle
            try { await RecalcStreakCacheAsync(streakId); }
            catch { /* log if logger injected in future */ }

            return new StreakMonthlyProgressDto
            {
                StreakId = streakId,
                YearMonth = yearMonth,
                DaysMask = progress.DaysMask,
            };
        }

        public async Task<List<StreakMonthlyProgressDto>> GetProgressAsync(
            int userId, int streakId, string fromMonth, string toMonth)
        {
            var rows = await _context.StreakMonthlyProgress
                .Where(p => p.StreakId == streakId
                         && p.UserId == userId
                         && string.Compare(p.YearMonth, fromMonth) >= 0
                         && string.Compare(p.YearMonth, toMonth) <= 0)
                .OrderBy(p => p.YearMonth)
                .Select(p => new StreakMonthlyProgressDto
                {
                    StreakId = p.StreakId,
                    YearMonth = p.YearMonth,
                    DaysMask = p.DaysMask,
                })
                .ToListAsync();

            return rows;
        }

        // ─── Study Sessions ───────────────────────────────────────────────────────

        /// <summary>
        /// Upserts daily aggregate records. Each aggregate covers a full day, so
        /// existing rows are replaced with the client's computed totals.
        /// PerStreak keys are client streak IDs — mapped to server IDs here.
        /// </summary>
        public async Task UpsertAggregatesAsync(
            int userId,
            List<DailyAggregateRequest> aggregates,
            Dictionary<string, int> clientToServerIdMap)
        {
            if (aggregates.Count == 0) return;

            foreach (var agg in aggregates)
            {
                if (!DateOnly.TryParseExact(agg.Date, "yyyy-MM-dd", out var date))
                    continue;

                // Remap per-streak keys from client IDs to server IDs
                string? perStreakJson = null;
                if (agg.PerStreak != null && agg.PerStreak.Count > 0)
                {
                    var serverPerStreak = new Dictionary<string, int>();
                    foreach (var (clientId, secs) in agg.PerStreak)
                    {
                        var key = clientToServerIdMap.TryGetValue(clientId, out var serverId)
                            ? serverId.ToString()
                            : clientId;
                        serverPerStreak[key] = serverPerStreak.TryGetValue(key, out var prev)
                            ? prev + secs
                            : secs;
                    }
                    perStreakJson = JsonSerializer.Serialize(serverPerStreak);
                }

                var existing = await _context.DailyStudySummary
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date);

                if (existing == null)
                {
                    _context.DailyStudySummary.Add(new DailyStudySummary
                    {
                        UserId = userId,
                        Date = date,
                        TotalSeconds = agg.TotalSeconds,
                        CdSeconds = agg.CdSeconds,
                        SwSeconds = agg.SwSeconds,
                        SessionCount = agg.SessionCount,
                        PerStreak = perStreakJson,
                    });
                }
                else
                {
                    // Replace with client's authoritative daily totals
                    existing.TotalSeconds = agg.TotalSeconds;
                    existing.CdSeconds = agg.CdSeconds;
                    existing.SwSeconds = agg.SwSeconds;
                    existing.SessionCount = agg.SessionCount;
                    if (perStreakJson != null)
                        existing.PerStreak = perStreakJson;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<DailySummaryDto>> GetSummaryAsync(int userId, DateOnly from, DateOnly to)
        {
            var rows = await _context.DailyStudySummary
                .Where(s => s.UserId == userId && s.Date >= from && s.Date <= to)
                .OrderBy(s => s.Date)
                .ToListAsync();

            return rows.Select(r => new DailySummaryDto
            {
                Date = r.Date,
                TotalSeconds = r.TotalSeconds,
                CdSeconds = r.CdSeconds,
                SwSeconds = r.SwSeconds,
                SessionCount = r.SessionCount,
                PerStreak = r.PerStreak != null
                    ? JsonSerializer.Deserialize<Dictionary<string, int>>(r.PerStreak)
                    : null,
            }).ToList();
        }

        // ─── Full Sync (first-time migration) ────────────────────────────────────

        /// <summary>
        /// Bulk upserts all client data on first login after feature release.
        /// Returns a map of clientId → server StreakId so the client can persist the mapping.
        /// </summary>
        public async Task<Dictionary<string, int>> FullSyncAsync(int userId, FullSyncRequest req)
        {
            var clientToServerId = new Dictionary<string, int>();

            // 1. Upsert streaks
            foreach (var streakReq in req.Streaks)
            {
                var dto = await CreateStreakAsync(userId, streakReq);
                clientToServerId[streakReq.ClientId] = dto.StreakId;
            }

            // 2. Upsert monthly progress bitmasks
            foreach (var p in req.Progress)
            {
                if (!clientToServerId.TryGetValue(p.ClientStreakId, out var serverStreakId))
                    continue;

                var existing = await _context.StreakMonthlyProgress
                    .FirstOrDefaultAsync(x => x.StreakId == serverStreakId && x.YearMonth == p.YearMonth);

                if (existing == null)
                {
                    _context.StreakMonthlyProgress.Add(new StreakMonthlyProgress
                    {
                        StreakId = serverStreakId,
                        UserId = userId,
                        YearMonth = p.YearMonth,
                        DaysMask = p.DaysMask,
                    });
                }
                else
                {
                    // OR the masks so neither device loses data
                    existing.DaysMask |= p.DaysMask;
                }
            }

            await _context.SaveChangesAsync();

            // Recalc cache for all synced streaks
            foreach (var serverId in clientToServerId.Values)
                await RecalcStreakCacheAsync(serverId);

            // 3. Upsert daily aggregates
            await UpsertAggregatesAsync(userId, req.Aggregates, clientToServerId);

            // Update user timezone if provided
            if (!string.IsNullOrWhiteSpace(req.Timezone))
            {
                await _context.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Timezone, req.Timezone));
            }

            return clientToServerId;
        }

    }
}
