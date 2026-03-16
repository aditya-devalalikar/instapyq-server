namespace pqy_server.Models.Year
{
    public class YearDto
    {
        public int YearId { get; set; }
        public string? YearName { get; set; }
        public string? PaperName { get; set; }
        public int ExamId { get; set; }
        public string ExamName { get; set; }   // 👈 Extra property for projection
        public int YearOrder { get; set; }
        public bool IsPremium { get; set; }
        public bool IsDeleted { get; set; }
        public int QuestionCount { get; set; }
        public string? QuestionPaperUrl { get; set; }
        public string? AnswerKeyUrl { get; set; }
    }
}
