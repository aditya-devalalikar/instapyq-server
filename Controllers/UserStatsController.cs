using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Helpers;
using pqy_server.Shared;
using pqy_server.Data;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize] // 🔐 Requires authenticated user
    [ApiController]
    [Route("api/[controller]")]
    public class UserStatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserStatsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets statistics for the authenticated user including login streak and attempt stats.
        /// </summary>
        /// <returns>Structured user stats including streak and accuracy.</returns>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyStats()
        {
            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID in token."));

            // 1. LOGIN STREAK CALCULATION
            var today = IstHelper.NowIst().Date;
            var loginDates = await _context.UserActivities
                .Where(a => a.UserId == userId && a.ActivityType == "login")
                .Select(a => a.ActivityTime)
                .ToListAsync();

            // Convert stored UTC times to IST dates before comparing
            var loginIstDates = loginDates
                .Select(t => TimeZoneInfo.ConvertTimeFromUtc(t, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")).Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            int loginStreak = 0;
            DateTime currentDay = today;
            foreach (var date in loginIstDates)
            {
                if (date == currentDay)
                {
                    loginStreak++;
                    currentDay = currentDay.AddDays(-1);
                }
                else if (date < currentDay)
                {
                    break; // Streak ended
                }
            }

            // 2. ATTEMPT STATISTICS (convert UTC activity times to IST for correct day boundaries)
            var ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            var nowIst = IstHelper.NowIst();
            var startOfToday = nowIst.Date;
            var startOfWeek = nowIst.Date.AddDays(-(int)nowIst.DayOfWeek);
            var startOfMonth = new DateTime(nowIst.Year, nowIst.Month, 1);

            var activities = await _context.UserActivities
                .Where(a => a.UserId == userId && a.ActivityType == "attempt")
                .ToListAsync();

            int todayAttempts = activities.Count(a => TimeZoneInfo.ConvertTimeFromUtc(a.ActivityTime, ist).Date == startOfToday);
            int weekAttempts = activities.Count(a => TimeZoneInfo.ConvertTimeFromUtc(a.ActivityTime, ist).Date >= startOfWeek);
            int monthAttempts = activities.Count(a => TimeZoneInfo.ConvertTimeFromUtc(a.ActivityTime, ist).Date >= startOfMonth);

            int correctAttempts = activities.Count(a => a.IsCorrect == true);
            int totalAttempts = activities.Count;
            double accuracy = totalAttempts > 0 ? ((double)correctAttempts / totalAttempts) * 100 : 0;

            // Compose result object
            var result = new
            {
                loginStreak,
                attempts = new
                {
                    today = todayAttempts,
                    week = weekAttempts,
                    month = monthAttempts,
                    total = totalAttempts,
                    correct = correctAttempts,
                    wrong = totalAttempts - correctAttempts,
                    accuracy = Math.Round(accuracy, 2)
                }
            };

            return Ok(ApiResponse<object>.Success(result));
        }
    }
}
