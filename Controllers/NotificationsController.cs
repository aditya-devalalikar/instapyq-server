using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Notifications;
using pqy_server.Services;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;

// Aliases for clarity
using FcmNotification = FirebaseAdmin.Messaging.Notification;
using LocalNotification = pqy_server.Models.Notifications.Notification;

namespace pqy_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FcmNotificationService _fcmNotificationService;
        private readonly INotificationService _notificationService;

        public NotificationsController(AppDbContext context, FcmNotificationService fcmNotificationService, INotificationService notificationService)
        {
            _context = context;
            _fcmNotificationService = fcmNotificationService;
            _notificationService = notificationService;
        }

        // 📬 GET: /api/notifications
        // Returns notifications visible to current user and general (UserId=null)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var notifications = await _context.Notifications
                    .Where(n => n.UserId == null || n.UserId == userId) // fetch broadcast + personal
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

            return Ok(ApiResponse<object>.Success(notifications));
        }

        // ✅ POST: /api/notifications/mark-read/{id}
        // Allows user to mark notification as read
        [Authorize]
        [HttpPost("mark-read/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && (n.UserId == userId || n.UserId == null));

            if (notification == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Notification not found."));

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Marked as read"));
        }

        // ❌ DELETE: /api/notifications/{id}
        // Allows user to delete their notification
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Notification not found."));

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Notification deleted"));
        }

        // 🧪 POST: /api/notifications/test-fcm
        // Any authenticated user — sends a test FCM push to their own device token.
        // Use this to verify the full FCM pipeline is working end-to-end.
        [Authorize]
        [HttpPost("test-fcm")]
        public async Task<IActionResult> TestFcm()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            if (string.IsNullOrWhiteSpace(user.FcmToken))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "No FCM token on file for this user. Open the app fresh so the token is registered."));

            try
            {
                var msgId = await _fcmNotificationService.SendNotificationAsync(
                    user.FcmToken,
                    "🔥 FCM Test",
                    "If you see this, push notifications are working correctly!"
                );
                return Ok(ApiResponse<string>.Success($"FCM delivered. Message ID: {msgId}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "test-fcm failed for userId={UserId}", userId);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, $"FCM send failed: {ex.Message}"));
            }
        }

        // 🔊 POST: /api/notifications/send
        // Admin-only: Sends notification(s) to a specific user or all users
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] CreateNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Title and message are required."));

            if (request.UserId.HasValue)
            {
                var notification = new LocalNotification
                {
                    UserId = request.UserId.Value,
                    Title = request.Title,
                    Message = request.Message
                };
                _context.Notifications.Add(notification);
            }
            else
            {
                var users = await _context.Users.ToListAsync();
                var notifications = users.Select(u => new LocalNotification
                {
                    UserId = u.UserId,
                    Title = request.Title,
                    Message = request.Message
                }).ToList();

                _context.Notifications.AddRange(notifications);
            }

            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.Success("Notification(s) sent successfully."));
        }

        // ✅ POST: /api/notifications/send-to-user
        // Admin-only: Send notification via service to a specific user with push, etc.
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("send-to-user")]
        public async Task<IActionResult> SendToUser([FromBody] CreateNotificationRequest request)
        {
            if (request.UserId == null)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "UserId is required for this endpoint."));

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Title and message are required."));

            try
            {
                await _notificationService.SendNotificationToUserAsync(
                    request.UserId.Value,
                    request.Title,
                    request.Message
                );

                return Ok(ApiResponse<string>.Success($"Notification sent to user {request.UserId}."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Notify user failed. uid={uid}", request.UserId);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while sending the notification."));
            }
        }

        // ✅ POST: /api/notifications/broadcast
        // Admin-only: Broadcast notification to all users with FCM push
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("broadcast")]
        public async Task<IActionResult> Broadcast([FromBody] CreateNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Title and message are required."));

            var notification = new LocalNotification
            {
                UserId = null, // broadcast
                Title = request.Title,
                Message = request.Message,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ✅ Send FCM to all users in batches (optional)
            var usersWithTokens = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.FcmToken))
                .Select(u => u.FcmToken)
                .ToListAsync();

            foreach (var token in usersWithTokens)
            {
                await _fcmNotificationService.SendNotificationAsync(token, request.Title, request.Message);
            }

            return Ok(ApiResponse<string>.Success("Broadcast sent successfully."));
        }


        // ✅ POST: /api/notifications/send-to-topic
        // Admin-only: Send notification to users subscribed to a topic (FCM)
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("send-to-topic")]
        public async Task<IActionResult> SendToTopic([FromBody] CreateTopicNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic) ||
                string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Topic, title, and message are required."));

            var message = new Message()
            {
                Topic = request.Topic,
                Notification = new FcmNotification
                {
                    Title = request.Title,
                    Body = request.Message
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);

            return Ok(ApiResponse<string>.Success($"Notification sent to topic '{request.Topic}'."));
        }
    }
}
