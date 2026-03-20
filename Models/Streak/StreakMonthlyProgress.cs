namespace pqy_server.Models.Streak
{
    // ─── Entity ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores one month of streak completion data as a bitmask.
    /// Bit N (0-based) = day (N+1) of the month is completed.
    /// e.g. DaysMask = 5 (binary 101) means day 1 and day 3 are completed.
    /// Composite PK: (StreakId, YearMonth).
    /// </summary>
    public class StreakMonthlyProgress
    {
        public int StreakId { get; set; }
        public Streak Streak { get; set; } = null!;

        /// <summary>Denormalized for fast user-scoped queries without join.</summary>
        public int UserId { get; set; }

        /// <summary>Format: 'YYYY-MM' e.g. '2026-03'</summary>
        public string YearMonth { get; set; } = string.Empty;

        /// <summary>
        /// 32-bit bitmask. Bit index = day - 1.
        /// Day 1 = bit 0 (value 1), Day 31 = bit 30 (value 1073741824).
        /// </summary>
        public int DaysMask { get; set; } = 0;
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class StreakMonthlyProgressDto
    {
        public int StreakId { get; set; }
        public string YearMonth { get; set; } = string.Empty;
        public int DaysMask { get; set; }
    }

    // ─── Requests ────────────────────────────────────────────────────────────────

    public class ToggleProgressRequest
    {
        /// <summary>Local date string from client. Format: 'YYYY-MM-DD'.</summary>
        public string Date { get; set; } = string.Empty;
    }

    public class ProgressSyncItem
    {
        public string ClientStreakId { get; set; } = string.Empty;
        public string YearMonth { get; set; } = string.Empty;
        public int DaysMask { get; set; }
    }
}
