namespace pqy_server.Models.Leaderboard
{
    /// <summary>
    /// What metric is being ranked.
    /// </summary>
    public enum LeaderboardType
    {
        // --- Questions attempted (UserDailyProgress) ---
        Questions,          // total questions attempted

        // --- Accuracy (UserDailyProgress, min 10 attempts) ---
        Accuracy,           // overall accuracy %

        // --- Exam-mode (ExamProgress.CompletedAt != null) ---
        Exams,              // most exams completed (all types)
        ExamsYear,          // most Year-mode exams
        ExamsSubject,       // most Subject-mode exams
        ExamsTopic,         // most Topic-mode exams

        AccuracyExams,      // best avg accuracy across exam-mode (all types, min 3 exams)
        AccuracyYear,       // best avg accuracy in Year-mode exams
        AccuracySubject,    // best avg accuracy in Subject-mode exams

        // --- Consistency (UserDailyProgress) ---
        Streak,             // longest consecutive study streak
        Consistency,        // % of days active this month (active days / days in month)
    }

    /// <summary>
    /// Time window filter for the leaderboard.
    /// </summary>
    public enum LeaderboardPeriod
    {
        Today,
        Week,
        Month,
        Year,
        AllTime,
    }

    // ─── Response DTOs ───────────────────────────────────────────────────────

    public class LeaderboardEntry
    {
        public int Rank       { get; set; }
        public int UserId     { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Avatar  { get; set; }         // Google profile pic or null
        public double Score   { get; set; }          // raw value (attempts, %, days…)
        public string ScoreLabel { get; set; } = string.Empty; // e.g. "342 questions"
        public bool IsMe      { get; set; }
    }

    public class MyRankEntry
    {
        public int    Rank       { get; set; }   // 0 = not ranked (no data)
        public double Score      { get; set; }
        public string ScoreLabel { get; set; } = string.Empty;
    }

    public class LeaderboardResponse
    {
        public List<LeaderboardEntry> Items   { get; set; } = new();
        public MyRankEntry?           MyRank  { get; set; }
        public int                    Total   { get; set; }
        public int                    Page    { get; set; }
        public int                    PageSize { get; set; }
    }

    // ─── Batch response ───────────────────────────────────────────────────────

    /// <summary>One board's top-N slice + the requesting user's rank.</summary>
    public class BoardData
    {
        public List<LeaderboardEntry> Items  { get; set; } = new();
        public MyRankEntry?           MyRank { get; set; }
        public int                    Total  { get; set; }
    }

    /// <summary>
    /// All board types for a given period in one response.
    /// Keys are the lowercase enum name (e.g. "questions", "examsyear").
    /// </summary>
    public class BatchLeaderboardResponse
    {
        public Dictionary<string, BoardData> Boards { get; set; } = new();
    }

    // ─── Internal projection (used inside service) ────────────────────────────

    internal class RankedScore
    {
        public int    UserId { get; set; }
        public double Score  { get; set; }
    }
}
