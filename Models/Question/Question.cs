using pqy_server.Enums;
using pqy_server.Models.Exams;
using pqy_server.Models.Subjects;
using pqy_server.Models.Topics;
using System.ComponentModel.DataAnnotations.Schema;
using static pqy_server.Enums.QuestionEnums;
using YearEntity = pqy_server.Models.Years.Year;
using TopicEntity = pqy_server.Models.Topics.Topic;

namespace pqy_server.Models.Questions
{
    public class Question
    {
        public int QuestionId { get; set; }

        public int? ExamId { get; set; }
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }

        public int? SubjectId { get; set; }
        [ForeignKey("SubjectId")]
        public Subject? Subject { get; set; }

        public int? TopicId { get; set; }
        [ForeignKey("TopicId")]
        public TopicEntity? Topic { get; set; }

        public int? YearId { get; set; }

        [ForeignKey("YearId")]
        public YearEntity Year { get; set; }

        public string? QuestionText { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; }
        public string? Explanation { get; set; }
        public string? Source { get; set; }
        public QuestionEnums.SourceType? SourceType { get; set; }
        public QuestionEnums.DifficultyLevel? DifficultyLevel { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public QuestionEnums.Nature? Nature { get; set; }
        public string? Motivation { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool IsOfficialAnswer { get; set; } = false;
    }

    public class QuestionDto
    {
        public int QuestionId { get; set; }

        public int? ExamId { get; set; }
        public string? ExamName { get; set; }

        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }

        public int? TopicId { get; set; }
        public string? TopicName { get; set; }

        public int? YearId { get; set; }
        public string? YearName { get; set; }

        public string? QuestionText { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectOption { get; set; }
        public string? Explanation { get; set; }

        public string? Source { get; set; }
        public EnumDto? SourceType { get; set; }
        public EnumDto? DifficultyLevel { get; set; }
        public EnumDto? Nature { get; set; }
        public string? Motivation { get; set; }

        public bool IsDeleted { get; set; }
        public bool IsOfficialAnswer { get; set; }
        public bool isBookmarked { get; set; }

        public DateTime UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        // ✅ Images
        public string? QuestionImageUrl { get; set; }
        public List<string> AnswerImageUrls { get; set; } = new();
        public string? OptionAImageUrl { get; set; }
        public string? OptionBImageUrl { get; set; }
        public string? OptionCImageUrl { get; set; }
        public string? OptionDImageUrl { get; set; }
    }

    public class EnumDto
    {
        public int Value { get; set; }
        public string Key { get; set; } = null!;
        public string Label { get; set; } = null!;
    }
}
