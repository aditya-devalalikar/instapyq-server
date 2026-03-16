namespace pqy_server.Models.Activity
{
    public class UserStatsResponse
    {
        public int TotalAttempts { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public double AccuracyPercentage { get; set; }

        public int TodayAttempts { get; set; }
        public int WeeklyAttempts { get; set; }
        public int MonthlyAttempts { get; set; }

        public int LoginStreakDays { get; set; }
    }
}
