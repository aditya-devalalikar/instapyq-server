using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Notifications
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int? UserId { get; set; } // null = sent to all users

        public string Title { get; set; }
        public string? Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
