using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using pqy_server.Models.Questions; // <-- This must contain Subject & Topic
using pqy_server.Models.Subjects;
using pqy_server.Models.Topics;
using UserEntity = pqy_server.Models.Users.User;
using TopicModel = pqy_server.Models.Topics.Topic;


namespace pqy_server.Models.Bookmark
{
    public class BookmarkQuestion
    {
        [Key]
        public int BookmarkId { get; set; }

        public int UserId { get; set; }
        public int QuestionId { get; set; }
        public int? SubjectId { get; set; }
        public int? TopicId { get; set; }

        [ForeignKey(nameof(UserId))]
        public UserEntity User { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public Question Question { get; set; }

        [ForeignKey(nameof(SubjectId))]
        public Subject? Subject { get; set; } // ✅ Singular, matches the class

        [ForeignKey(nameof(TopicId))]
        public TopicModel? Topic { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
