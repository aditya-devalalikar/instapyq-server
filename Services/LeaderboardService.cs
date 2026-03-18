using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using pqy_server.Data;
using pqy_server.Helpers;
using pqy_server.Models.Leaderboard;
using pqy_server.Models.Order;
using pqy_server.Models.Orders;
using pqy_server.Models.Users;
using pqy_server.Shared;

namespace pqy_server.Services
{
    public interface ILeaderboardService
    {
        Task<LeaderboardResponse> GetLeaderboardAsync(
            LeaderboardType type,
            LeaderboardPeriod period,
            int page,
            int pageSize,
            int requestingUserId,
            DateOnly? date = null);

        Task<BatchLeaderboardResponse> GetBatchAsync(
            LeaderboardPeriod period,
            int pageSize,
            int requestingUserId,
            DateOnly? date = null);
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        // Cache TTLs per period — fresher for shorter windows
        private static readonly Dictionary<LeaderboardPeriod, TimeSpan> CacheTtl = new()
        {
            [LeaderboardPeriod.Today]   = TimeSpan.FromMinutes(3),
            [LeaderboardPeriod.Week]    = TimeSpan.FromMinutes(10),
            [LeaderboardPeriod.Month]   = TimeSpan.FromMinutes(20),
            [LeaderboardPeriod.Year]    = TimeSpan.FromMinutes(60),
            [LeaderboardPeriod.AllTime] = TimeSpan.FromMinutes(60),
        };

        private const int MinAttemptsForAccuracy = 10;
        private const int MinExamsForAccuracy    = 3;

        // Pre-fetch and cache eligible user IDs to avoid a correlated subquery
        // on every ranking computation. Cached for 5 minutes; stale at most one
        // leaderboard refresh cycle behind the actual premium roster.
        private async Task<HashSet<int>> GetEligibleUserIdsAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeys.LeaderboardEligibleUserIds, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var ids = await _context.Users
                    .Where(u => !u.HideFromLeaderboard && !u.IsDeleted
                        && _context.Orders.Any(o => o.UserId == u.UserId
                            && o.Status == OrderStatus.Paid
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow))
                    .AsNoTracking()
                    .Select(u => u.UserId)
                    .ToListAsync();
                return ids.ToHashSet();
            }) ?? new HashSet<int>();
        }

        public LeaderboardService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache   = cache;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public entry point
        // ─────────────────────────────────────────────────────────────────────

        public async Task<LeaderboardResponse> GetLeaderboardAsync(
            LeaderboardType type,
            LeaderboardPeriod period,
            int page,
            int pageSize,
            int requestingUserId,
            DateOnly? date = null)
        {
            var effectivePeriod = (type == LeaderboardType.Streak)
                ? LeaderboardPeriod.AllTime
                : period;

            var cacheKey = date.HasValue
                ? $"lb:{type}:{effectivePeriod}:{date.Value:yyyy-MM-dd}"
                : $"lb:{type}:{effectivePeriod}";

            // Historical dates are immutable — cache them for 24 h
            var today = DateOnly.FromDateTime(IstHelper.NowIst());
            var ttl = date.HasValue && date.Value < today
                ? TimeSpan.FromHours(24)
                : CacheTtl[effectivePeriod];

            var allRanked = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ttl;
                return await ComputeRankingAsync(type, effectivePeriod, date);
            }) ?? new List<RankedScore>();

            int total = allRanked.Count;

            var pageSlice = allRanked
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var idsToFetch = pageSlice.Select(r => r.UserId).ToHashSet();
            idsToFetch.Add(requestingUserId);

            var users = await _context.Users
                .Where(u => idsToFetch.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Username, u.GoogleProfilePicture })
                .AsNoTracking()
                .ToDictionaryAsync(u => u.UserId);

            var items = pageSlice.Select((r, i) =>
            {
                int rank = (page - 1) * pageSize + i + 1;
                users.TryGetValue(r.UserId, out var u);
                return new LeaderboardEntry
                {
                    Rank       = rank,
                    UserId     = r.UserId,
                    Username   = u?.Username ?? "Unknown",
                    Avatar     = u?.GoogleProfilePicture,
                    Score      = r.Score,
                    ScoreLabel = FormatScore(type, r.Score),
                    IsMe       = r.UserId == requestingUserId,
                };
            }).ToList();

            MyRankEntry? myRank = null;
            var myEntry = allRanked.FirstOrDefault(r => r.UserId == requestingUserId);
            if (myEntry != null)
            {
                int myPosition = allRanked.IndexOf(myEntry) + 1;
                myRank = new MyRankEntry
                {
                    Rank       = myPosition,
                    Score      = myEntry.Score,
                    ScoreLabel = FormatScore(type, myEntry.Score),
                };
            }

            return new LeaderboardResponse
            {
                Items    = items,
                MyRank   = myRank,
                Total    = total,
                Page     = page,
                PageSize = pageSize,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Ranking dispatch
        // ─────────────────────────────────────────────────────────────────────

        private Task<List<RankedScore>> ComputeRankingAsync(
            LeaderboardType type, LeaderboardPeriod period, DateOnly? date = null) => type switch
        {
            LeaderboardType.Questions       => QuestionsRanking(period, date),
            LeaderboardType.Accuracy        => AccuracyRanking(period, date),
            LeaderboardType.Exams           => ExamsRanking(period, modeType: null, date),
            LeaderboardType.ExamsYear       => ExamsRanking(period, "Year", date),
            LeaderboardType.ExamsSubject    => ExamsRanking(period, "Subject", date),
            LeaderboardType.ExamsTopic      => ExamsRanking(period, "Topic", date),
            LeaderboardType.AccuracyExams   => ExamAccuracyRanking(period, modeType: null, date),
            LeaderboardType.AccuracyYear    => ExamAccuracyRanking(period, "Year", date),
            LeaderboardType.AccuracySubject => ExamAccuracyRanking(period, "Subject", date),
            LeaderboardType.Streak          => StreakRanking(),
            LeaderboardType.Consistency     => ConsistencyRanking(period, date),
            _                               => Task.FromResult(new List<RankedScore>()),
        };

        // ─── Questions attempted ──────────────────────────────────────────────

        private async Task<List<RankedScore>> QuestionsRanking(LeaderboardPeriod period, DateOnly? date = null)
        {
            var (start, end) = GetDateRange(period, date);
            var eligibleIds = await GetEligibleUserIdsAsync();

            return await _context.UserDailyProgress
                .Where(p => (start == null || p.Date >= start) &&
                            (end   == null || p.Date <= end) &&
                            eligibleIds.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new RankedScore
                {
                    UserId = g.Key,
                    Score  = g.Sum(p => p.Attempts),
                })
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .AsNoTracking()
                .ToListAsync();
        }

        // ─── Overall accuracy ─────────────────────────────────────────────────
        // Math.Round with 2 args doesn't translate on PostgreSQL float8.
        // Aggregate in SQL, compute percentage in memory.

        private async Task<List<RankedScore>> AccuracyRanking(LeaderboardPeriod period, DateOnly? date = null)
        {
            var (start, end) = GetDateRange(period, date);
            var eligibleIds = await GetEligibleUserIdsAsync();

            var raw = await _context.UserDailyProgress
                .Where(p => (start == null || p.Date >= start) &&
                            (end   == null || p.Date <= end) &&
                            eligibleIds.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId   = g.Key,
                    Attempts = g.Sum(p => p.Attempts),
                    Correct  = g.Sum(p => p.Correct),
                })
                .Where(g => g.Attempts >= MinAttemptsForAccuracy)
                .AsNoTracking()
                .ToListAsync();

            return raw
                .Select(g => new RankedScore
                {
                    UserId = g.UserId,
                    Score  = Math.Round((double)g.Correct / g.Attempts * 100, 1),
                })
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        // ─── Exam count ───────────────────────────────────────────────────────

        private async Task<List<RankedScore>> ExamsRanking(
            LeaderboardPeriod period, string? modeType, DateOnly? date = null)
        {
            var (startDate, endDate) = GetDateRange(period, date);
            DateTime? start = startDate.HasValue
                ? startDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            DateTime? end = endDate.HasValue
                ? endDate.Value.ToDateTime(new TimeOnly(23, 59, 59)) : null;

            var eligibleIds = await GetEligibleUserIdsAsync();

            var raw = await _context.ExamProgress
                .Where(ep => ep.CompletedAt != null
                    && (modeType == null || ep.ModeType == modeType)
                    && (start    == null || ep.CompletedAt >= start)
                    && (end      == null || ep.CompletedAt <= end)
                    && eligibleIds.Contains(ep.UserId))
                .GroupBy(ep => ep.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .Where(g => g.Count > 0)
                .AsNoTracking()
                .ToListAsync();

            return raw
                .Select(g => new RankedScore { UserId = g.UserId, Score = g.Count })
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        // ─── Exam accuracy ────────────────────────────────────────────────────
        // Same Math.Round issue — aggregate in SQL, divide in memory.

        private async Task<List<RankedScore>> ExamAccuracyRanking(
            LeaderboardPeriod period, string? modeType, DateOnly? date = null)
        {
            var (startDate, endDate) = GetDateRange(period, date);
            DateTime? start = startDate.HasValue
                ? startDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            DateTime? end = endDate.HasValue
                ? endDate.Value.ToDateTime(new TimeOnly(23, 59, 59)) : null;

            var eligibleIds = await GetEligibleUserIdsAsync();

            var raw = await _context.ExamProgress
                .Where(ep => ep.CompletedAt != null
                    && ep.AttemptedCount > 0
                    && (modeType == null || ep.ModeType == modeType)
                    && (start    == null || ep.CompletedAt >= start)
                    && (end      == null || ep.CompletedAt <= end)
                    && eligibleIds.Contains(ep.UserId))
                .GroupBy(ep => ep.UserId)
                .Select(g => new
                {
                    UserId         = g.Key,
                    ExamCount      = g.Count(),
                    TotalCorrect   = g.Sum(ep => ep.CorrectCount),
                    TotalAttempted = g.Sum(ep => ep.AttemptedCount),
                })
                .Where(g => g.ExamCount >= MinExamsForAccuracy)
                .AsNoTracking()
                .ToListAsync();

            return raw
                .Select(g => new RankedScore
                {
                    UserId = g.UserId,
                    Score  = Math.Round((double)g.TotalCorrect / g.TotalAttempted * 100, 1),
                })
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        // ─── Streak ───────────────────────────────────────────────────────────

        private async Task<List<RankedScore>> StreakRanking()
        {
            var eligibleIds = await GetEligibleUserIdsAsync();

            var pairs = await _context.UserDailyProgress
                .Where(p => eligibleIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.Date })
                .Distinct()
                .OrderBy(p => p.UserId)
                .ThenBy(p => p.Date)
                .AsNoTracking()
                .ToListAsync();

            return pairs
                .GroupBy(p => p.UserId)
                .Select(g =>
                {
                    var dates = g.Select(x => x.Date).ToList();
                    int max = 1, cur = 1;
                    for (int i = 1; i < dates.Count; i++)
                    {
                        int diff = dates[i].DayNumber - dates[i - 1].DayNumber;
                        cur = diff == 1 ? cur + 1 : 1;
                        if (cur > max) max = cur;
                    }
                    return new RankedScore { UserId = g.Key, Score = max };
                })
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        // ─── Consistency ──────────────────────────────────────────────────────
        // g.Select(p => p.Date).Distinct().Count() doesn't translate to SQL in
        // EF Core. Fix: project to distinct (UserId,Date) pairs first, then group.

        private async Task<List<RankedScore>> ConsistencyRanking(LeaderboardPeriod period, DateOnly? date = null)
        {
            var (startOpt, endOpt) = GetDateRange(period, date);
            var nowIst = IstHelper.NowIst();
            var today  = DateOnly.FromDateTime(nowIst);
            var start  = startOpt ?? new DateOnly(nowIst.Year, nowIst.Month, 1);
            var end    = endOpt   ?? today;

            int totalDays = end.DayNumber - start.DayNumber + 1;
            if (totalDays <= 0) return new List<RankedScore>();

            // Fetch distinct (UserId, Date) pairs first — EF Core + PostgreSQL cannot
            // translate Distinct().GroupBy() as a single query reliably.
            var eligibleIds = await GetEligibleUserIdsAsync();

            var pairs = await _context.UserDailyProgress
                .Where(p => p.Date >= start && p.Date <= end &&
                            eligibleIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.Date })
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            return pairs
                .GroupBy(p => p.UserId)
                .Select(g => new RankedScore
                {
                    UserId = g.Key,
                    Score  = Math.Round((double)g.Count() / totalDays * 100, 1),
                })
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        private static (DateOnly? Start, DateOnly? End) GetDateRange(
            LeaderboardPeriod period, DateOnly? referenceDate = null)
        {
            var nowIst = IstHelper.NowIst();
            var today  = DateOnly.FromDateTime(nowIst);

            if (referenceDate is DateOnly refDate)
            {
                // The frontend always sends the period-start as refDate:
                //   today  → the specific day
                //   week   → Sunday of that week
                //   month  → 1st of that month
                //   year   → Jan 1 of that year
                // Cap end at today so future dates never appear.
                return period switch
                {
                    LeaderboardPeriod.Today => (refDate, refDate),
                    LeaderboardPeriod.Week  => (refDate, DateOnly.FromDayNumber(
                                                    Math.Min(refDate.AddDays(6).DayNumber, today.DayNumber))),
                    LeaderboardPeriod.Month => (refDate, DateOnly.FromDayNumber(
                                                    Math.Min(new DateOnly(refDate.Year, refDate.Month,
                                                        DateTime.DaysInMonth(refDate.Year, refDate.Month)).DayNumber,
                                                        today.DayNumber))),
                    LeaderboardPeriod.Year  => (refDate, DateOnly.FromDayNumber(
                                                    Math.Min(new DateOnly(refDate.Year, 12, 31).DayNumber,
                                                        today.DayNumber))),
                    _ => (null, null),
                };
            }

            return period switch
            {
                LeaderboardPeriod.Today   => (today, today),
                LeaderboardPeriod.Week    => (today.AddDays(-(int)nowIst.DayOfWeek), today),
                LeaderboardPeriod.Month   => (new DateOnly(nowIst.Year, nowIst.Month, 1), today),
                LeaderboardPeriod.Year    => (new DateOnly(nowIst.Year, 1, 1), today),
                LeaderboardPeriod.AllTime => (null, null),
                _                         => (null, null),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Batch — all board types for a given period, sequentially
        //
        // NOTE: EF Core's DbContext is NOT thread-safe, so concurrent Task.WhenAll
        // across 11 board types causes "second operation started before previous
        // completed" errors.  Sequential awaits are safe and still fast because
        // each type's ranked list is served from IMemoryCache after the first call.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<BatchLeaderboardResponse> GetBatchAsync(
            LeaderboardPeriod period,
            int pageSize,
            int requestingUserId,
            DateOnly? date = null)
        {
            var response = new BatchLeaderboardResponse();

            foreach (var type in Enum.GetValues<LeaderboardType>())
            {
                try
                {
                    var res = await GetLeaderboardAsync(type, period, 1, pageSize, requestingUserId, date);
                    response.Boards[type.ToString().ToLowerInvariant()] = new BoardData
                    {
                        Items  = res.Items,
                        MyRank = res.MyRank,
                        Total  = res.Total,
                    };
                }
                catch
                {
                    // Swallow individual board failures — return empty board rather than 500
                    response.Boards[type.ToString().ToLowerInvariant()] = new BoardData();
                }
            }

            return response;
        }

        private static string FormatScore(LeaderboardType type, double score) => type switch
        {
            LeaderboardType.Questions                                              => $"{(int)score} questions",
            LeaderboardType.Accuracy or LeaderboardType.AccuracyExams or
            LeaderboardType.AccuracyYear or LeaderboardType.AccuracySubject       => $"{score:0.#}% accuracy",
            LeaderboardType.Exams or LeaderboardType.ExamsYear or
            LeaderboardType.ExamsSubject or LeaderboardType.ExamsTopic            => $"{(int)score} exams",
            LeaderboardType.Streak                                                => $"{(int)score} day streak",
            LeaderboardType.Consistency                                           => $"{score:0.#}% consistent",
            _                                                                     => score.ToString("0.#"),
        };
    }
}
