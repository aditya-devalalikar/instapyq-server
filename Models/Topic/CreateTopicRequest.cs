using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Topics
{
    public class CreateTopicRequest
    {
        [MaxLength(200)]
        public string? TopicName { get; set; }

        public int SubjectId { get; set; }
        public int TopicOrder { get; set; }
    }
}
