using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Activity
{
    public class CreateUserActivityRequest
    {
        public int QuestionId { get; set; }

        [Required]
        [MaxLength(1)]
        public string AnsweredOption { get; set; }

        public bool IsCorrect { get; set; }
    }
}
