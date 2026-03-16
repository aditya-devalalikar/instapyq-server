using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Notifications
{
    public class CreateTopicNotificationRequest
    {
        [Required]
        [MaxLength(100)]
        public string Topic { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }
    }
}
