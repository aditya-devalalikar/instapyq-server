namespace pqy_server.Models.Admin
{
    public class AdminStats
    {
        public int TotalUsers { get; set; }
        public int TotalExams { get; set; }
        public int TotalPyqs { get; set; }
        public int TotalSubjects { get; set; }
        public int TotalTopics { get; set; }
        public int TotalYears { get; set; }
        public int PremiumUsers { get; set; }
        public object Roles { get; set; } = new();
        public IEnumerable<object> ExamAnalysis { get; set; }
        public IEnumerable<object> SubjectStats { get; set; }
        public int GlobalMissingExplanation { get; set; }
        public int GlobalMissingOfficialAnswers { get; set; }

        public IEnumerable<object> YearCoverageStats { get; set; }

        public List<DailyUpdateStat> DailyUpdates { get; set; }
        public List<MonthlyUpdateStat> MonthlyUpdates { get; set; }
    }

    public class DailyUpdateStat
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyUpdateStat
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }
    }

}
