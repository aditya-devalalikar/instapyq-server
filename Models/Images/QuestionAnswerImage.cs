using pqy_server.Models.Questions;

namespace pqy_server.Models.Images
{
    public class QuestionAnswerImage
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;

        public int ImageId { get; set; }
        public ImageFile Image { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
