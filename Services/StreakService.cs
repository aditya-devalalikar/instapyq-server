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
        Task UpsertSessionsAsync(int userId, List<StudySessionRequest> sessions, Dictionary<string, int> clientToServerIdMap);
        Task<List<DailySummaryDto>> GetSummaryAsync(int userId, DateOnly from, DateOnly to);

        // Full first-time sync
        Task<Dictionary<string, int>> FullSyncAsync(int userId, FullSyncRequest req);

        // Maintenance
        Task NullOldSessionsAsync();
    }

    public class StreakService : IStreakService
    {
        private readonly AppDbContext _context;

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

            // Current streak: only valid if the last completed date is today or yesterday
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var lastDate = dates[^1];
            int currentStreak = (lastDate == today || lastDate == today.AddDays(-1)) ? current : 0;

            await _context.Streaks
                .Where(s => s.StreakId == streakId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.CurrentStreakDays, currentStreak)
                    .SetProperty(x => x.LongestStreakDays, longest)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
        }

        /// <summary>Checks whether a session with the given clientSessionId exists in the Sessions JSON array.</summary>
        private static bool SessionExistsInJson(string? sessionsJson, string clientSessionId)
        {
            if (string.IsNullOrEmpty(sessionsJson)) return false;
            try
            {
                using var doc = JsonDocument.Parse(sessionsJson);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("clientSessionId", out var prop) &&
                        prop.GetString() == clientSessionId)
                        return true;
                }
                return false;
            }
            catch { return false; }
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
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.ClientId == req.ClientId && s.UserId == userId);

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return ToDto(existing);
            }

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

            // Hard-delete the streak itself (ignoring soft-delete filter)
            await _context.Streaks
                .IgnoreQueryFilters()
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
            await RecalcStreakCacheAsync(streakId);

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

        public async Task UpsertSessionsAsync(
            int userId,
            List<StudySessionRequest> sessions,
            Dictionary<string, int> clientToServerIdMap)
        {
            if (sessions.Count == 0) return;

            foreach (var session in sessions)
            {
                if (!DateOnly.TryParseExact(session.Date, "yyyy-MM-dd", out var date))
                    continue;

                // Resolve client streak ID → server streak ID
                int? serverStreakId = null;
                if (!string.IsNullOrEmpty(session.ClientStreakId)
                    && clientToServerIdMap.TryGetValue(session.ClientStreakId, out var sid))
                {
                    serverStreakId = sid;
                }

                var sessionItem = new SessionItemDto
                {
                    ClientSessionId = session.ClientSessionId,
                    StreakId = serverStreakId,
                    StreakName = session.StreakName,
                    DurationSeconds = session.DurationSeconds,
                    Mode = session.Mode,
                    StartedAt = session.StartedAt,
                };
                var sessionJson = JsonSerializer.Serialize(sessionItem);

                var perStreakKey = serverStreakId?.ToString();

                var existing = await _context.DailyStudySummary
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date);

                if (existing == null)
                {
                    // New day row
                    var perStreak = serverStreakId.HasValue
                        ? JsonSerializer.Serialize(new Dictionary<string, int>
                            { [perStreakKey!] = session.DurationSeconds })
                        : null;

                    _context.DailyStudySummary.Add(new DailyStudySummary
                    {
                        UserId = userId,
                        Date = date,
                        TotalSeconds = session.DurationSeconds,
                        SessionCount = 1,
                        PerStreak = perStreak,
                        Sessions = $"[{sessionJson}]",
                    });
                }
                else
                {
                    // Dedup: skip if session already saved
                    if (SessionExistsInJson(existing.Sessions, session.ClientSessionId))
                        continue;

                    // Append session to Sessions JSON array
                    if (existing.Sessions == null)
                    {
                        existing.Sessions = JsonSerializer.Serialize(new List<SessionItemDto> { sessionItem });
                    }
                    else
                    {
                        var list = JsonSerializer.Deserialize<List<SessionItemDto>>(existing.Sessions)
                            ?? new List<SessionItemDto>();
                        list.Add(sessionItem);
                        existing.Sessions = JsonSerializer.Serialize(list);
                    }

                    // Accumulate totals
                    existing.TotalSeconds += session.DurationSeconds;
                    existing.SessionCount++;

                    // Update per-streak breakdown
                    if (serverStreakId.HasValue)
                    {
                        var perStreak = existing.PerStreak != null
                            ? JsonSerializer.Deserialize<Dictionary<string, int>>(existing.PerStreak)
                              ?? new Dictionary<string, int>()
                            : new Dictionary<string, int>();

                        var key = serverStreakId.Value.ToString();
                        perStreak[key] = perStreak.TryGetValue(key, out var prev)
                            ? prev + session.DurationSeconds
                            : session.DurationSeconds;

                        existing.PerStreak = JsonSerializer.Serialize(perStreak);
                    }
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
                SessionCount = r.SessionCount,
                PerStreak = r.PerStreak != null
                    ? JsonSerializer.Deserialize<Dictionary<string, int>>(r.PerStreak)
                    : null,
                Sessions = r.Sessions != null
                    ? JsonSerializer.Deserialize<List<SessionItemDto>>(r.Sessions)
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

            // 3. Upsert sessions
            await UpsertSessionsAsync(userId, req.Sessions, clientToServerId);

            // Update user timezone if provided
            if (!string.IsNullOrWhiteSpace(req.Timezone))
            {
                await _context.Users
                    .Where(u => u.UserId == userId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Timezone, req.Timezone));
            }

            return clientToServerId;
        }

        // ─── Maintenance ──────────────────────────────────────────────────────────

        /// <summary>Nulls the Sessions JSONB column for rows older than 30 days.</summary>
        public async Task NullOldSessionsAsync()
        {
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            await _context.DailyStudySummary
                .Where(s => s.Date < cutoff && s.Sessions != null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Sessions, (string?)null));
        }
    }
}
