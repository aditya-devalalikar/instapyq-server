using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Helpers;
using pqy_server.Models.Progress;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProgressController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProgressController(AppDbContext context)
        {
            _context = context;
        }

        // POST: /api/progress/sync
        [HttpPost("sync")]
        public async Task<IActionResult> SyncAttempts([FromBody] AttemptBatch batch)
        {
            const int MaxBatchSize = 100;
            if (batch == null || batch.Attempts.Count == 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "No attempts provided."));

            if (batch.Attempts.Count > MaxBatchSize)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError,
                    $"Batch size cannot exceed {MaxBatchSize} attempts per sync."));

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or missing User ID."));

            try
            {
                var groups = batch.Attempts
                    .Where(a => a.SubjectId.HasValue && a.ExamId.HasValue)
                    .GroupBy(a =>
                    {
                        DateTimeOffset.TryParse(a.Timestamp, null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed);
                        return new
                        {
                            Date = DateOnly.FromDateTime(parsed == DateTimeOffset.MinValue
                                ? IstHelper.NowIst() : TimeZoneInfo.ConvertTimeFromUtc(parsed.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"))),
                            SubjectId = a.SubjectId!.Value,
                            ExamId = a.ExamId!.Value
                        };
                    })
                    .Select(g => new
                    {
                        g.Key.Date,
                        g.Key.SubjectId,
                        g.Key.ExamId,
                        Attempts = g.Count(),
                        Correct = g.Count(a => a.IsCorrect)
                    })
                    .ToList();

                foreach (var group in groups)
                {
                    var existing = await _context.UserDailyProgress
                        .FindAsync(userId, group.Date, group.SubjectId, group.ExamId);

                    if (existing != null)
                    {
                        existing.Attempts += group.Attempts;
                        existing.Correct += group.Correct;
                    }
                    else
                    {
                        _context.UserDailyProgress.Add(new UserDailyProgress
                        {
                            UserId = userId,
                            Date = group.Date,
                            SubjectId = group.SubjectId,
                            ExamId = group.ExamId,
                            Attempts = group.Attempts,
                            Correct = group.Correct
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(ApiResponse<object>.Success(new { Count = groups.Count }, "Attempts synced successfully."));
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.DatabaseError, "Failed to save attempts."));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An unexpected error occurred."));
            }
        }

        // DELETE: /api/progress/clear
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAttempts()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<object>.Failure(ResultCode.Unauthorized, "Invalid or missing User ID."));

            try
            {
                var deletedCount = await _context.UserDailyProgress
                    .Where(p => p.UserId == userId)
                    .ExecuteDeleteAsync();

                return Ok(ApiResponse<object>.Success(new { Count = deletedCount }, "All attempts cleared successfully."));
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, ApiResponse<object>.Failure(ResultCode.DatabaseError, "Database error while clearing attempts."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Clear attempts failed.");
                return StatusCode(500, ApiResponse<object>.Failure(ResultCode.InternalServerError, "An error occurred while clearing attempts."));
            }
        }

        // GET: /api/progress/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or missing User ID."));

                var nowIst = IstHelper.NowIst();
                var today = DateOnly.FromDateTime(nowIst);
                var startOfWeek = today.AddDays(-(int)nowIst.DayOfWeek);
                var startOfMonth = new DateOnly(nowIst.Year, nowIst.Month, 1);
                var startOfYear = new DateOnly(nowIst.Year, 1, 1);

                var allData = await _context.UserDailyProgress
                    .Where(p => p.UserId == userId && p.Date >= startOfYear)
                    .AsNoTracking()
                    .ToListAsync();

                if (allData.Count == 0)
                    return Ok(ApiResponse<ProgressSummaryResponse>.Success(new ProgressSummaryResponse(), "No data found."));

                var subjectIds = allData.Select(p => p.SubjectId).Distinct().ToList();
                var examIds = allData.Select(p => p.ExamId).Distinct().ToList();

                var subjectNames = await _context.Subjects
                    .Where(s => subjectIds.Contains(s.SubjectId))
                    .ToDictionaryAsync(s => s.SubjectId, s => s.SubjectName);

                var examNames = await _context.Exams
                    .Where(e => examIds.Contains(e.ExamId))
                    .ToDictionaryAsync(e => e.ExamId, e => e.ExamName);

                ProgressSummary BuildSummary(IEnumerable<UserDailyProgress> set, DateOnly start, DateOnly end,
                    bool includeWeek = false, bool includeYear = false)
                {
                    var list = set.Where(p => p.Date >= start && p.Date <= end).ToList();
                    var totalAttempts = list.Sum(p => p.Attempts);
                    var totalCorrect = list.Sum(p => p.Correct);

                    var summary = new ProgressSummary
                    {
                        Attempts = totalAttempts,
                        Correct = totalCorrect,
                        Wrong = totalAttempts - totalCorrect,
                        Accuracy = totalAttempts > 0
                            ? Math.Round((double)totalCorrect / totalAttempts * 100, 2) : 0,
                        SubjectStats = list
                            .GroupBy(p => p.SubjectId)
                            .ToDictionary(
                                g => subjectNames.GetValueOrDefault(g.Key, "Unknown"),
                                g => g.Sum(p => p.Attempts)),
                        ExamStats = list
                            .GroupBy(p => p.ExamId)
                            .ToDictionary(
                                g => examNames.GetValueOrDefault(g.Key, "Unknown"),
                                g => g.Sum(p => p.Attempts)),
                        Contribution = list
                            .GroupBy(p => p.Date)
                            .Select(g => new DailyCommit
                            {
                                Date = g.Key.ToString("yyyy-MM-dd"),
                                Count = g.Sum(p => p.Attempts)
                            })
                            .ToList()
                    };

                    if (includeWeek)
                        summary.WeeklyHistory = Enumerable.Range(0, 7)
                            .Select(i => list.Where(p => p.Date == startOfWeek.AddDays(i)).Sum(p => p.Attempts))
                            .ToList();

                    if (includeYear)
                        summary.MonthlyCounts = Enumerable.Range(1, 12)
                            .Select(m => list.Where(p => p.Date.Month == m).Sum(p => p.Attempts))
                            .ToList();

                    return summary;
                }

                var response = new ProgressSummaryResponse
                {
                    Today = BuildSummary(allData, today, today),
                    Week = BuildSummary(allData, startOfWeek, today, includeWeek: true),
                    Month = BuildSummary(allData, startOfMonth, today),
                    Year = BuildSummary(allData, startOfYear, today, includeYear: true)
                };

                return Ok(ApiResponse<ProgressSummaryResponse>.Success(response, "All summaries generated successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Progress summary failed.");
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while generating the summary."));
            }
        }

        // POST: /api/progress/exam-progress
        [HttpPost("exam-progress")]
        public async Task<IActionResult> SaveExamProgress([FromBody] ExamProgress dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or missing User ID."));

            var progress = new ExamProgress
            {
                ExamProgressId = dto.ExamProgressId,
                UserId = userId,
                ModeType = dto.ModeType,
                YearId = dto.YearId,
                SubjectIds = dto.SubjectIds,
                TopicIds = dto.TopicIds,
                QuestionCount = dto.QuestionCount,
                AttemptedCount = dto.AttemptedCount,
                CorrectCount = dto.CorrectCount,
                WrongCount = dto.WrongCount,
                SkippedCount = dto.SkippedCount,
                Elim1 = dto.Elim1,
                Elim1Correct = dto.Elim1Correct,
                Elim1Wrong = dto.Elim1Wrong,
                Elim2 = dto.Elim2,
                Elim2Correct = dto.Elim2Correct,
                Elim2Wrong = dto.Elim2Wrong,
                Elim3 = dto.Elim3,
                Elim3Correct = dto.Elim3Correct,
                Elim3Wrong = dto.Elim3Wrong,
                StartedAt = dto.StartedAt,
                CompletedAt = dto.CompletedAt
            };

            _context.ExamProgress.Add(progress);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<ExamProgress>.Success(progress, "Exam progress saved successfully"));
        }

        // GET: /api/progress/exam-summary
        [HttpGet("exam-summary")]
        public async Task<IActionResult> GetExamSummary()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid or missing User ID."));

            var exams = await _context.ExamProgress
                .Where(e => e.UserId == userId)
                .ToListAsync();

            if (!exams.Any())
                return Ok(ApiResponse<ExamSummaryResponse>.Success(new ExamSummaryResponse(), "No exams found."));

            var now = DateTime.UtcNow;
            var today = now.Date;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            // ── Completed vs all ──────────────────────────────────────────────
            var completed = exams.Where(e => e.CompletedAt.HasValue).ToList();
            var completionRate = exams.Count > 0
                ? Math.Round((double)completed.Count / exams.Count * 100, 1) : 0;

            // ── Timing stats (completed exams only) ───────────────────────────
            var completedWithDuration = completed
                .Where(e => (e.CompletedAt!.Value - e.StartedAt).TotalSeconds > 0)
                .ToList();
            var avgDurationSeconds = completedWithDuration.Any()
                ? Math.Round(completedWithDuration.Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalSeconds), 0) : 0;
            var avgTimePerQuestion = completedWithDuration.Any(e => e.QuestionCount > 0)
                ? Math.Round(completedWithDuration.Where(e => e.QuestionCount > 0)
                    .Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalSeconds / e.QuestionCount), 1) : 0;

            // ── Skip rate + overall accuracy ──────────────────────────────────
            var avgSkipRate = exams.Any(e => e.QuestionCount > 0)
                ? Math.Round(exams.Where(e => e.QuestionCount > 0)
                    .Average(e => (double)e.SkippedCount / e.QuestionCount * 100), 1) : 0;
            var totalAttempted = exams.Sum(e => e.AttemptedCount);
            var totalCorrect = exams.Sum(e => e.CorrectCount);
            var overallAccuracy = totalAttempted > 0
                ? Math.Round((double)totalCorrect / totalAttempted * 100, 1) : 0;

            // ── Today count ───────────────────────────────────────────────────
            var todayExams = completed.Count(e => e.CompletedAt!.Value.Date == today);

            // ── Size stats (with best score) ──────────────────────────────────
            var sizeCategories = new[] { 5, 10, 25, 50, 100 };
            var sizeStats = sizeCategories.ToDictionary(
                size => size,
                size =>
                {
                    var filtered = exams.Where(e => e.QuestionCount == size).ToList();
                    return new ExamSizeStat
                    {
                        Count = filtered.Count,
                        AverageScore = filtered.Any()
                            ? Math.Round(filtered.Average(e => (double)e.CorrectCount / e.QuestionCount * 100), 2) : 0,
                        BestScore = filtered.Any()
                            ? Math.Round(filtered.Max(e => (double)e.CorrectCount / e.QuestionCount * 100), 2) : 0
                    };
                });

            // ── Mode type stats (Year / Subject / Topic) ──────────────────────
            var modeTypeStats = new[] { "Year", "Subject", "Topic" }.ToDictionary(
                mode => mode,
                mode =>
                {
                    var modeExams = exams.Where(e => e.ModeType == mode).ToList();
                    return new ExamModeStat
                    {
                        Count = modeExams.Count,
                        AverageScore = modeExams.Any(e => e.QuestionCount > 0)
                            ? Math.Round(modeExams.Where(e => e.QuestionCount > 0)
                                .Average(e => (double)e.CorrectCount / e.QuestionCount * 100), 1) : 0
                    };
                });

            // ── Elimination stats ─────────────────────────────────────────────
            var elim1Decided = exams.Sum(e => e.Elim1Correct + e.Elim1Wrong);
            var elim2Decided = exams.Sum(e => e.Elim2Correct + e.Elim2Wrong);
            var elim3Decided = exams.Sum(e => e.Elim3Correct + e.Elim3Wrong);
            var eliminationStats = new ExamEliminationStats
            {
                Elim1Uses = exams.Sum(e => e.Elim1),
                Elim1Accuracy = elim1Decided > 0
                    ? Math.Round((double)exams.Sum(e => e.Elim1Correct) / elim1Decided * 100, 1) : 0,
                Elim2Uses = exams.Sum(e => e.Elim2),
                Elim2Accuracy = elim2Decided > 0
                    ? Math.Round((double)exams.Sum(e => e.Elim2Correct) / elim2Decided * 100, 1) : 0,
                Elim3Uses = exams.Sum(e => e.Elim3),
                Elim3Accuracy = elim3Decided > 0
                    ? Math.Round((double)exams.Sum(e => e.Elim3Correct) / elim3Decided * 100, 1) : 0,
            };

            // ── Weekly history (count + avg score per day) ────────────────────
            var weeklyHistory = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var day = startOfWeek.AddDays(i);
                    return completed.Count(e => e.CompletedAt!.Value.Date == day);
                })
                .ToList();
            var weeklyAccuracy = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var day = startOfWeek.AddDays(i);
                    var dayExams = completed.Where(e => e.CompletedAt!.Value.Date == day && e.QuestionCount > 0).ToList();
                    return dayExams.Any()
                        ? Math.Round(dayExams.Average(e => (double)e.CorrectCount / e.QuestionCount * 100), 1) : 0.0;
                })
                .ToList();

            // ── Monthly history (12 months) ───────────────────────────────────
            var monthlyHistory = Enumerable.Range(1, 12)
                .Select(m => completed.Count(e =>
                    e.CompletedAt!.Value.Year == now.Year && e.CompletedAt.Value.Month == m))
                .ToList();

            // ── Score distribution ────────────────────────────────────────────
            var scoreDistribution = new Dictionary<string, int>
            {
                ["0-20"]   = exams.Count(e => e.QuestionCount > 0 && (double)e.CorrectCount / e.QuestionCount * 100 <= 20),
                ["20-40"]  = exams.Count(e => e.QuestionCount > 0 && (double)e.CorrectCount / e.QuestionCount * 100 is > 20 and <= 40),
                ["40-60"]  = exams.Count(e => e.QuestionCount > 0 && (double)e.CorrectCount / e.QuestionCount * 100 is > 40 and <= 60),
                ["60-80"]  = exams.Count(e => e.QuestionCount > 0 && (double)e.CorrectCount / e.QuestionCount * 100 is > 60 and <= 80),
                ["80-100"] = exams.Count(e => e.QuestionCount > 0 && (double)e.CorrectCount / e.QuestionCount * 100 > 80)
            };

            // ── Recent trend (last 20 completed, oldest → newest) ─────────────
            var recentTrend = completed
                .OrderByDescending(e => e.CompletedAt)
                .Take(20)
                .OrderBy(e => e.CompletedAt)
                .Select(e => new ExamTrendPoint
                {
                    Date  = e.CompletedAt!.Value.ToString("yyyy-MM-dd"),
                    Score = e.QuestionCount > 0
                        ? Math.Round((double)e.CorrectCount / e.QuestionCount * 100, 1) : 0,
                    Size  = e.QuestionCount,
                    Mode  = e.ModeType ?? "Year"
                })
                .ToList();

            return Ok(ApiResponse<ExamSummaryResponse>.Success(new ExamSummaryResponse
            {
                TotalExams         = exams.Count,
                CompletedExams     = completed.Count,
                CompletionRate     = completionRate,
                OverallAccuracy    = overallAccuracy,
                AvgDurationSeconds = avgDurationSeconds,
                AvgTimePerQuestion = avgTimePerQuestion,
                AvgSkipRate        = avgSkipRate,
                TodayExams         = todayExams,
                SizeStats          = sizeStats,
                ModeTypeStats      = modeTypeStats,
                EliminationStats   = eliminationStats,
                WeeklyHistory      = weeklyHistory,
                WeeklyAccuracy     = weeklyAccuracy,
                MonthlyHistory     = monthlyHistory,
                ScoreDistribution  = scoreDistribution,
                RecentTrend        = recentTrend,
            }, "Exam summary generated successfully."));
        }

        public class ExamSummaryResponse
        {
            public int TotalExams { get; set; } = 0;
            public int CompletedExams { get; set; } = 0;
            public double CompletionRate { get; set; } = 0;
            public double OverallAccuracy { get; set; } = 0;
            public double AvgDurationSeconds { get; set; } = 0;
            public double AvgTimePerQuestion { get; set; } = 0;
            public double AvgSkipRate { get; set; } = 0;
            public int TodayExams { get; set; } = 0;
            public Dictionary<int, ExamSizeStat> SizeStats { get; set; } = new();
            public Dictionary<string, ExamModeStat> ModeTypeStats { get; set; } = new();
            public ExamEliminationStats EliminationStats { get; set; } = new();
            public List<int> WeeklyHistory { get; set; } = new();
            public List<double> WeeklyAccuracy { get; set; } = new();
            public List<int> MonthlyHistory { get; set; } = new();
            public Dictionary<string, int> ScoreDistribution { get; set; } = new();
            public List<ExamTrendPoint> RecentTrend { get; set; } = new();
        }

        public class ExamSizeStat
        {
            public int Count { get; set; }
            public double AverageScore { get; set; }
            public double BestScore { get; set; }
        }

        public class ExamModeStat
        {
            public int Count { get; set; }
            public double AverageScore { get; set; }
        }

        public class ExamEliminationStats
        {
            public int Elim1Uses { get; set; }
            public double Elim1Accuracy { get; set; }
            public int Elim2Uses { get; set; }
            public double Elim2Accuracy { get; set; }
            public int Elim3Uses { get; set; }
            public double Elim3Accuracy { get; set; }
        }

        public class ExamTrendPoint
        {
            public string Date { get; set; } = "";
            public double Score { get; set; }
            public int Size { get; set; }
            public string Mode { get; set; } = "";
        }
    }
}