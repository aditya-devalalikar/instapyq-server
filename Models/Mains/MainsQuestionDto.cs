namespace pqy_server.Models.Mains
{
    public class MainsQuestionDto
    {
        public int Id { get; set; }
        public string Year { get; set; }
        public int YearId { get; set; }
        public PaperType PaperType { get; set; }
        public int PaperNumber { get; set; }
        public string OptionalSubject { get; set; }
        public string? Section { get; set; }
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; }
        public int Marks { get; set; }
        public string Topic { get; set; }
        public int? TopicId { get; set; }
        public string Subject { get; set; }
        public int? SubjectId { get; set; }
        public string Language { get; set; }
    }
}
