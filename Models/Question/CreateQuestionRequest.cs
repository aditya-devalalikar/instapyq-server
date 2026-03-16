using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Questions
{
    public class CreateQuestionRequest
    {
        public int? ExamId { get; set; }
        public int? SubjectId { get; set; }
        public int? TopicId { get; set; }
        public int? YearId { get; set; }

        [MaxLength(2000)]
        public string? QuestionText { get; set; }

        [MaxLength(500)]
        public string? OptionA { get; set; }

        [MaxLength(500)]
        public string? OptionB { get; set; }

        [MaxLength(500)]
        public string? OptionC { get; set; }

        [MaxLength(500)]
        public string? OptionD { get; set; }

        [MaxLength(1)]
        public string? CorrectOption { get; set; }

        [MaxLength(5000)]
        public string? Explanation { get; set; }

        [MaxLength(500)]
        public string? QuestionImage { get; set; }

        public List<string>? AnswerImages { get; set; }

        [MaxLength(500)]
        public string? OptionAImageUrl { get; set; }

        [MaxLength(500)]
        public string? OptionBImageUrl { get; set; }

        [MaxLength(500)]
        public string? OptionCImageUrl { get; set; }

        [MaxLength(500)]
        public string? OptionDImageUrl { get; set; }

        [MaxLength(200)]
        public string? Source { get; set; }

        [MaxLength(50)]
        public string? SourceType { get; set; }

        [MaxLength(50)]
        public string? DifficultyLevel { get; set; }

        [MaxLength(100)]
        public string? Nature { get; set; }

        [MaxLength(500)]
        public string? Motivation { get; set; }

        public bool IsDeleted { get; set; } = false;
        public bool IsOfficialAnswer { get; set; } = false;
    }
}
