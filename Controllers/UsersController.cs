using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Order;
using pqy_server.Models.User;
using pqy_server.Models.Users;
using pqy_server.Services;
using pqy_server.Shared;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace pqy_server.Controllers
{
    [ApiController]
    [Authorize] // 👤 Any authenticated user
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IStreakAlertScheduleService _alertScheduleService;

        public UsersController(
            AppDbContext context,
            IMemoryCache cache,
            IStreakAlertScheduleService alertScheduleService)
        {
            _context = context;
            _cache = cache;
            _alertScheduleService = alertScheduleService;
        }

        // 👤 GET /api/users/{id}
        // Retrieves detailed information about a user by ID — Admin only
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.UserEmail,
                    Role = u.Role != null ? u.Role.RoleName : "Unknown",
                    IsPremium = _context.Orders.Any(o => o.UserId == u.UserId
                        && o.Status == OrderStatus.Paid
                        && o.ExpiresAt != null
                        && o.ExpiresAt > DateTime.UtcNow),
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            return Ok(ApiResponse<object>.Success(user));
        }

        // 👤 GET /api/users/me
        // Retrieves profile data for the authenticated user
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID not found in token."));

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId.ToString() == userId);

            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            // Use the same 30-min cache as QuestionsController to avoid a per-request Orders query
            var premiumCacheKey = $"user-premium-status:{user.UserId}";
            if (!_cache.TryGetValue(premiumCacheKey, out bool isPremium))
            {
                isPremium = await _context.Orders
                    .AsNoTracking()
                    .AnyAsync(o => o.UserId == user.UserId
                        && o.Status == OrderStatus.Paid
                        && o.ExpiresAt != null
                        && o.ExpiresAt > DateTime.UtcNow);
                _cache.Set(premiumCacheKey, isPremium, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    Size = 1
                });
            }

            var result = new MyProfileDto
            {
                UserId = user.UserId,
                Username = user.Username,
                UserEmail = user.UserEmail,
                Role = user.Role?.RoleName ?? "Unknown",
                IsPremium = isPremium,
                CreatedAt = user.CreatedAt,
                SelectedExamIds     = user.SelectedExamIds ?? new List<int>(),
                HideFromLeaderboard = user.HideFromLeaderboard,
                IsGoogleLoginOnly   = user.IsGoogleLoginOnly,
            };

            return Ok(ApiResponse<MyProfileDto>.Success(result));
        }

        // ✏️ PUT /api/users/me
        // Updates the profile information of the authenticated user
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID not found in token."));

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID invalid."));

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            // Check if email is being updated and is unique (not allowed for Google-only accounts)
            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.UserEmail)
            {
                if (user.IsGoogleLoginOnly)
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Email cannot be changed for Google accounts."));

                var emailUsed = await _context.Users.AnyAsync(u => u.UserEmail == request.Email && u.UserId != userId);
                if (emailUsed)
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "This email is already in use."));
            }

            // Update fields with provided values or keep existing
            user.Username = request.Username ?? user.Username;
            if (!user.IsGoogleLoginOnly)
                user.UserEmail = request.Email ?? user.UserEmail;
            user.UpdatedAt = DateTime.UtcNow;

            // Update selected exams if provided
            if (request.SelectedExamIds != null)
            {
                user.SelectedExamIds = request.SelectedExamIds;
                // Invalidate selected exam ids cache
                var selectedExamCacheKey = CacheKeys.UserSelectedExamIds(userId);
                _cache.Remove(selectedExamCacheKey);
            }

            if (request.HideFromLeaderboard.HasValue)
                user.HideFromLeaderboard = request.HideFromLeaderboard.Value;

            await _context.SaveChangesAsync();

            var isNowPremium = await _context.Orders
                .AnyAsync(o => o.UserId == user.UserId
                    && o.Status == OrderStatus.Paid
                    && o.ExpiresAt != null
                    && o.ExpiresAt > DateTime.UtcNow);

            var updatedUser = new
            {
                user.UserId,
                user.Username,
                user.UserEmail,
                Role = user.Role?.RoleName ?? "Unknown",
                IsPremium           = isNowPremium,
                user.CreatedAt,
                SelectedExamIds     = user.SelectedExamIds,
                user.HideFromLeaderboard,
                user.IsGoogleLoginOnly,
            };

            return Ok(ApiResponse<object>.Success(updatedUser));
        }

        // ✅ POST /api/users/me/update-fcm-token
        // Updates the Firebase Cloud Messaging token for push notifications
        [HttpPost("me/update-fcm-token")]
        public async Task<IActionResult> UpdateMyFcmToken([FromBody] UpdateFcmTokenRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID not found in token."));

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User ID invalid."));

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            var timezoneChanged =
                !string.IsNullOrWhiteSpace(request.Timezone) &&
                !string.Equals(user.Timezone, request.Timezone, StringComparison.Ordinal);

            user.FcmToken = request.FcmToken;
            if (timezoneChanged)
                user.Timezone = request.Timezone;
            user.UpdatedAt = DateTime.UtcNow;

            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();

            if (timezoneChanged)
            {
                await _alertScheduleService.ResyncSchedulesForUserAsync(userId);
                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();

            return Ok(ApiResponse<string>.Success("FCM token updated."));
        }
    }
}
