using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Mains
{
    public class CreateMainsQuestionDto
    {
        [Required] public int YearId { get; set; }
        [Required] public PaperType PaperType { get; set; }
        [Required] public int PaperNumber { get; set; }
        public OptionalSubject? OptionalSubject { get; set; }
        public string? Section { get; set; }
        [Required] public int QuestionNumber { get; set; }
        [Required] public string QuestionText { get; set; }
        [Required] public int Marks { get; set; }
        public int? SubjectId { get; set; }
        public int? TopicId { get; set; }
    }
}
