namespace pqy_server.Models.Progress
{
    public class AttemptLog
    {
        public string Timestamp { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }

        // All IDs as integers
        public int? QuestionId { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public int? ExamId { get; set; }
        public string? ExamName { get; set; }
        public int? YearId { get; set; }
        public string? YearName { get; set; }
    }

    public class AttemptBatch
    {
        public List<AttemptLog> Attempts { get; set; } = new();
    }
}
