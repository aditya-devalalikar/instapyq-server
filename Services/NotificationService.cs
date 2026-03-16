using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Models.Notifications;

namespace pqy_server.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly FcmNotificationService _fcmNotificationService;

        public NotificationService(AppDbContext context, FcmNotificationService fcmNotificationService)
        {
            _context = context;
            _fcmNotificationService = fcmNotificationService;
        }

        public async Task SendNotificationToUserAsync(int userId, string title, string message)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found.");
            }

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message
            };

            _context.Notifications.Add(notification);

            if (!string.IsNullOrWhiteSpace(user.FcmToken))
            {
                try
                {
                    var fcmResult = await _fcmNotificationService.SendNotificationAsync(
                        user.FcmToken,
                        title,
                        message
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NotificationService] Failed to send FCM notification: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[NotificationService] User does not have an FCM token.");
            }

            await _context.SaveChangesAsync();
        }
    }
}
