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

            var dto = await _streakService.CreateStreakAsync(GetUserId(), req);
            return Ok(ApiResponse<StreakDto>.Success(dto, "Streak created."));
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
        }

        /// <summary>DELETE /api/streak/{id} — Soft delete a streak.</summary>
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
        /// POST /api/streak/sessions/batch — Batch upsert study sessions.
        /// Free users: only last 7 days are saved.
        /// Premium users: full history saved.
        /// </summary>
        [HttpPost("sessions/batch")]
        public async Task<IActionResult> BatchSessions([FromBody] BatchSessionsRequest req)
        {
            if (!ModelState.IsValid || req.Sessions.Count == 0)
                return BadRequest(ApiResponse<object>.Failure(ResultCode.ValidationError, "Sessions list is empty or invalid."));

            var userId = GetUserId();
            var sessionsToSave = req.Sessions;

            // Free users: only save sessions from last 7 days
            if (!IsPremium())
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd");
                sessionsToSave = req.Sessions
                    .Where(s => string.Compare(s.Date, cutoff) >= 0)
                    .ToList();
            }

            // Build clientId → serverId map from existing user streaks
            var clientToServerId = await BuildClientIdMapAsync(userId);

            await _streakService.UpsertSessionsAsync(userId, sessionsToSave, clientToServerId);
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
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
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

            // Free users: strip sessions older than 7 days
            if (!IsPremium())
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd");
                req.Sessions = req.Sessions
                    .Where(s => string.Compare(s.Date, cutoff) >= 0)
                    .ToList();
            }

            var idMap = await _streakService.FullSyncAsync(userId, req);
            return Ok(ApiResponse<Dictionary<string, int>>.Success(idMap, "Sync complete."));
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
