using System.ComponentModel.DataAnnotations;
using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Streak
{
    // ─── Entity ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One row per user per day. Stores aggregated study totals.
    /// Composite PK: (UserId, Date).
    /// </summary>
    public class DailyStudySummary
    {
        public int UserId { get; set; }
        public UserEntity User { get; set; } = null!;

        public DateOnly Date { get; set; }

        public int TotalSeconds { get; set; } = 0;
        public int CdSeconds { get; set; } = 0;
        public int SwSeconds { get; set; } = 0;
        public short SessionCount { get; set; } = 0;

        /// <summary>JSON object mapping streak server ID → total seconds. e.g. {"5": 1800, "3": 2700}</summary>
        public string? PerStreak { get; set; }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class DailySummaryDto
    {
        public DateOnly Date { get; set; }
        public int TotalSeconds { get; set; }
        public int CdSeconds { get; set; }
        public int SwSeconds { get; set; }
        public short SessionCount { get; set; }
        public Dictionary<string, int>? PerStreak { get; set; }
    }

    // ─── Requests ────────────────────────────────────────────────────────────────

    public class DailyAggregateRequest
    {
        [Required]
        /// <summary>Local date from client. Format: 'YYYY-MM-DD'.</summary>
        public string Date { get; set; } = string.Empty;

        [Required]
        [Range(0, int.MaxValue)]
        public int TotalSeconds { get; set; }

        public int CdSeconds { get; set; }
        public int SwSeconds { get; set; }
        public short SessionCount { get; set; }

        public Dictionary<string, int>? PerStreak { get; set; }
    }

    public class BatchAggregatesRequest
    {
        [Required]
        public List<DailyAggregateRequest> Aggregates { get; set; } = new();
    }

    public class FullSyncRequest
    {
        public List<CreateStreakRequest> Streaks { get; set; } = new();
        public List<ProgressSyncItem> Progress { get; set; } = new();
        public List<DailyAggregateRequest> Aggregates { get; set; } = new();
        public string? Timezone { get; set; }
    }
}
