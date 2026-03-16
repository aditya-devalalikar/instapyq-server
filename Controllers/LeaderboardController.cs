using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pqy_server.Models.Leaderboard;
using pqy_server.Services;
using pqy_server.Shared;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        /// <summary>
        /// GET /api/leaderboard?type=Questions&amp;period=Today&amp;page=1&amp;pageSize=20
        ///
        /// Returns a paginated leaderboard for the given type + period.
        /// Response always includes:
        ///   - items[]          — current page entries (rank, user, score, isMe)
        ///   - myRank           — requesting user's position in the full list (null if no data)
        ///   - total / page / pageSize — pagination metadata
        ///
        /// Supported types (case-insensitive):
        ///   questions      — most questions attempted
        ///   accuracy       — highest accuracy % (min 10 attempts)
        ///   exams          — most exams completed (all modes)
        ///   examsyear      — most Year-mode exams
        ///   examssubject   — most Subject-mode exams
        ///   examstopic     — most Topic-mode exams
        ///   accuracyexams  — best avg accuracy in exam mode (min 3 exams)
        ///   accuracyyear   — best avg accuracy in Year-mode exams
        ///   accuracysubject— best avg accuracy in Subject-mode exams
        ///   streak         — longest consecutive study streak
        ///   consistency    — % of days active in the period
        ///
        /// Supported periods (case-insensitive):
        ///   today | week | month | year | alltime
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] string  type     = "questions",
            [FromQuery] string  period   = "today",
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            [FromQuery] string? date     = null)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            // Clamp page size: minimum 5, maximum 50
            pageSize = Math.Clamp(pageSize, 5, 50);
            page     = Math.Max(1, page);

            if (!Enum.TryParse<LeaderboardType>(type, ignoreCase: true, out var lbType))
                return BadRequest(ApiResponse<object>.Failure(
                    ResultCode.ValidationError, $"Unknown leaderboard type '{type}'."));

            if (!Enum.TryParse<LeaderboardPeriod>(period.Replace("-", ""), ignoreCase: true, out var lbPeriod))
                return BadRequest(ApiResponse<object>.Failure(
                    ResultCode.ValidationError, $"Unknown period '{period}'."));

            DateOnly? parsedDate = null;
            if (!string.IsNullOrEmpty(date) &&
                DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
                parsedDate = d;

            var result = await _leaderboardService.GetLeaderboardAsync(
                lbType, lbPeriod, page, pageSize, userId, parsedDate);

            return Ok(ApiResponse<LeaderboardResponse>.Success(result));
        }

        /// <summary>
        /// GET /api/leaderboard/batch?period=today&amp;pageSize=5
        ///
        /// Returns the top-N entries + the requesting user's rank for ALL board
        /// types in a single round-trip.  All boards are computed in parallel and
        /// each result is served from the per-type memory cache where possible.
        /// </summary>
        [HttpGet("batch")]
        public async Task<IActionResult> GetBatch(
            [FromQuery] string  period   = "today",
            [FromQuery] int     pageSize = 5,
            [FromQuery] string? date     = null)
        {
            if (!TryGetUserId(out int userId))
                return Unauthorized();

            pageSize = Math.Clamp(pageSize, 3, 10);

            if (!Enum.TryParse<LeaderboardPeriod>(period.Replace("-", ""), ignoreCase: true, out var lbPeriod))
                return BadRequest(ApiResponse<object>.Failure(
                    ResultCode.ValidationError, $"Unknown period '{period}'."));

            DateOnly? parsedDate = null;
            if (!string.IsNullOrEmpty(date) &&
                DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
                parsedDate = d;

            var result = await _leaderboardService.GetBatchAsync(lbPeriod, pageSize, userId, parsedDate);
            return Ok(ApiResponse<BatchLeaderboardResponse>.Success(result));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(str) && int.TryParse(str, out userId);
        }
    }
}
