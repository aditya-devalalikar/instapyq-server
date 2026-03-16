using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace pqy_server.Models.Topics
{
    public partial class Topic
    {
        [Key]
        public int TopicId { get; set; }

        public string? TopicName { get; set; }

        public int SubjectId { get; set; }

        public int TopicOrder { get; set; }

        public string? SubjectName { get; set; }

        [NotMapped]
        public int QuestionCount { get; set; }
    }
}
