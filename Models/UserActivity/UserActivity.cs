using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using pqy_server.Models.Questions;
using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Activity
{
    public class UserActivity
    {
        [Key]
        public int ActivityId { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public UserEntity User { get; set; }

        public int QuestionId { get; set; }
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }

        public string ActivityType { get; set; } = "attempt"; // or "login"

        public string? AnsweredOption { get; set; }
        public bool? IsCorrect { get; set; }

        public DateTime ActivityTime { get; set; } = DateTime.UtcNow;
    }
}
