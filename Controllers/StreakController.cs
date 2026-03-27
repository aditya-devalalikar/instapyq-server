using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using pqy_server.Models.Streak;
using pqy_server.Services;
using pqy_server.Shared;

namespace pqy_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StreakController : ControllerBase
    {
        private readonly IStreakService _streakService;

        public StreakController(IStreakService streakService)
        {
            _streakService = streakService;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsPremium() =>
            bool.Parse(User.FindFirstValue("isPremium") ?? "false");

        // ─── Streaks CRUD ─────────────────────────────────────────────────────────

        /// <summary>GET /api/streak — Fetch all streaks for the authenticated user.</summary>
        [HttpGet]
        public async Task<IActionResult> GetStreaks()
        {
            var streaks = await _streakService.GetStreaksAsync(GetUserId());
            return Ok(ApiResponse<List<StreakDto>>.Success(streaks));
        }

        /// <summary>POST /api/streak — Create a new streak.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateStreak([FromBody] CreateStreakRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Invalid request."));

            try
            {
                var dto = await _streakService.CreateStreakAsync(GetUserId(), req);
                return Ok(ApiResponse<StreakDto>.Success(dto, "Streak created."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, ex.Message));
            }
        }

        /// <summary>PUT /api/streak/{id} — Update an existing streak.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateStreak(int id, [FromBody] UpdateStreakRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Invalid request."));

            try
            {
                var dto = await _streakService.UpdateStreakAsync(GetUserId(), id, req);
                return Ok(ApiResponse<StreakDto>.Success(dto, "Streak updated."));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(ApiResponse<object>.Failure(ResultCode.NotFound, "Streak not found."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, ex.Message));
            }
        }

        /// <summary>DELETE /api/streak/{id} — Permanently delete a streak and all its associated data.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteStreak(int id)
        {
            await _streakService.DeleteStreakAsync(GetUserId(), id);
            return Ok(ApiResponse<object>.Success(null, "Streak deleted."));
        }

        // ─── Progress ─────────────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/streak/{id}/progress — Toggle a day's completion.
        /// Body: { "date": "2026-03-20" }
        /// </summary>
        [HttpPost("{id:int}/progress")]
        public async Task<IActionResult> ToggleProgress(int id, [FromBody] ToggleProgressRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Date))
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Date is required."));

            try
            {
                var result = await _streakService.ToggleProgressAsync(GetUserId(), id, req.Date);
                return Ok(ApiResponse<StreakMonthlyProgressDto>.Success(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, ex.Message));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(ApiResponse<object>.Failure(ResultCode.NotFound, "Streak not found."));
            }
        }

        /// <summary>
        /// GET /api/streak/{id}/progress?from=2026-01&amp;to=2026-03
        /// Returns bitmasks for the given month range.
        /// </summary>
        [HttpGet("{id:int}/progress")]
        public async Task<IActionResult> GetProgress(int id, [FromQuery] string from, [FromQuery] string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "from and to are required (format: YYYY-MM)."));

            var result = await _streakService.GetProgressAsync(GetUserId(), id, from, to);
            return Ok(ApiResponse<List<StreakMonthlyProgressDto>>.Success(result));
        }

        // ─── Study Sessions ───────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/streak/sessions/batch — Batch upsert daily study aggregates.
        /// Free users: only last 7 days are saved.
        /// Premium users: full history saved.
        /// </summary>
        [HttpPost("sessions/batch")]
        public async Task<IActionResult> BatchSessions([FromBody] BatchAggregatesRequest req)
        {
            if (!ModelState.IsValid || req.Aggregates.Count == 0)
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Aggregates list is empty or invalid."));

            var userId = GetUserId();
            var toSave = req.Aggregates;

            // Free users: only save aggregates from last 7 days
            if (!IsPremium())
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8)).ToString("yyyy-MM-dd");
                toSave = req.Aggregates
                    .Where(a => string.Compare(a.Date, cutoff) >= 0)
                    .ToList();
            }

            // Build clientId → serverId map from existing user streaks
            var clientToServerId = await BuildClientIdMapAsync(userId);

            await _streakService.UpsertAggregatesAsync(userId, toSave, clientToServerId);
            return Ok(ApiResponse<object>.Success(null, "Sessions synced."));
        }

        /// <summary>
        /// GET /api/streak/sessions/summary?from=2026-03-01&amp;to=2026-03-20
        /// Returns daily summaries for analytics screens.
        /// Free users: capped to last 7 days.
        /// </summary>
        [HttpGet("sessions/summary")]
        public async Task<IActionResult> GetSummary([FromQuery] string from, [FromQuery] string to)
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var fromDate)
             || !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var toDate))
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Invalid date format. Use YYYY-MM-DD."));

            if (!IsPremium())
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8));
                if (fromDate < cutoff) fromDate = cutoff;
            }

            var result = await _streakService.GetSummaryAsync(GetUserId(), fromDate, toDate);
            return Ok(ApiResponse<List<DailySummaryDto>>.Success(result));
        }

        // ─── Full Sync ────────────────────────────────────────────────────────────

        /// <summary>
        /// POST /api/streak/sync — One-time full sync from client local storage.
        /// Sends all streaks, progress, and sessions in one call.
        /// Returns clientId → serverId mapping so client can persist it.
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> FullSync([FromBody] FullSyncRequest req)
        {
            var userId = GetUserId();

            // Free users: strip aggregates older than 7 days
            if (!IsPremium())
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8)).ToString("yyyy-MM-dd");
                req.Aggregates = req.Aggregates
                    .Where(a => string.Compare(a.Date, cutoff) >= 0)
                    .ToList();
            }

            try
            {
                var idMap = await _streakService.FullSyncAsync(userId, req);
                return Ok(ApiResponse<Dictionary<string, int>>.Success(idMap, "Sync complete."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, ex.Message));
            }
        }

        // ─── Private helpers ──────────────────────────────────────────────────────

        /// <summary>Builds a clientId → serverId dictionary from the user's existing streaks.</summary>
        private async Task<Dictionary<string, int>> BuildClientIdMapAsync(int userId)
        {
            var streaks = await _streakService.GetStreaksAsync(userId);
            return streaks.ToDictionary(s => s.ClientId, s => s.StreakId);
        }
    }
}
