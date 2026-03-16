namespace pqy_server.Models.Progress
{
    // Single summary for a period
    public class ProgressSummary
    {
        public int Attempts { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public double Accuracy { get; set; }

        public Dictionary<string, int> SubjectStats { get; set; } = new(); // subject-wise counts
        public Dictionary<string, int> ExamStats { get; set; } = new();    // exam-wise counts

        public List<int> WeeklyHistory { get; set; } = new();   // Sun-Sat counts
        public List<int> MonthlyCounts { get; set; } = new();   // Jan-Dec counts
        public List<DailyCommit> Contribution { get; set; } = new(); // date-wise contributions
    }

    // Daily commit object for contribution graph
    public class DailyCommit
    {
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public int Count { get; set; }
    }

    // Wrapper DTO to return all required summaries in one response
    public class ProgressSummaryResponse
    {
        public ProgressSummary Today { get; set; } = new();
        public ProgressSummary Week { get; set; } = new();
        public ProgressSummary Month { get; set; } = new();
        public ProgressSummary Year { get; set; } = new();
    }
}
