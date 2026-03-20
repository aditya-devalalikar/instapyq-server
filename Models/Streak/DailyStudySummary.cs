using System.ComponentModel.DataAnnotations;
using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Streak
{
    // ─── Entity ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One row per user per day. Stores aggregated study totals and raw session
    /// list as JSONB. Sessions column is nulled after 30 days (archive).
    /// Composite PK: (UserId, Date).
    /// </summary>
    public class DailyStudySummary
    {
        public int UserId { get; set; }
        public UserEntity User { get; set; } = null!;

        public DateOnly Date { get; set; }

        public int TotalSeconds { get; set; } = 0;
        public short SessionCount { get; set; } = 0;

        /// <summary>JSON object mapping streak server ID → total seconds. e.g. {"5": 1800, "3": 2700}</summary>
        public string? PerStreak { get; set; }

        /// <summary>
        /// JSON array of raw session objects. Nulled after 30 days.
        /// Only used for "Sessions Today" list UI.
        /// </summary>
        public string? Sessions { get; set; }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class DailySummaryDto
    {
        public DateOnly Date { get; set; }
        public int TotalSeconds { get; set; }
        public short SessionCount { get; set; }
        public Dictionary<string, int>? PerStreak { get; set; }
        public List<SessionItemDto>? Sessions { get; set; }
    }

    public class SessionItemDto
    {
        public string ClientSessionId { get; set; } = string.Empty;
        public int? StreakId { get; set; }
        public string? StreakName { get; set; }
        public int DurationSeconds { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string? StartedAt { get; set; }
    }

    // ─── Requests ────────────────────────────────────────────────────────────────

    public class StudySessionRequest
    {
        [Required]
        [MaxLength(100)]
        /// <summary>Client-generated UUID. Used for deduplication.</summary>
        public string ClientSessionId { get; set; } = string.Empty;

        /// <summary>Client-side streak ID (MMKV key). Mapped to server StreakId on arrival.</summary>
        public string? ClientStreakId { get; set; }

        [MaxLength(100)]
        public string? StreakName { get; set; }

        [Required]
        /// <summary>Local date from client. Format: 'YYYY-MM-DD'.</summary>
        public string Date { get; set; } = string.Empty;

        [Required]
        [Range(5, int.MaxValue, ErrorMessage = "Session must be at least 5 seconds.")]
        public int DurationSeconds { get; set; }

        [Required]
        public string Mode { get; set; } = string.Empty;

        /// <summary>Display time for session list. Format: 'HH:mm'.</summary>
        public string? StartedAt { get; set; }
    }

    public class BatchSessionsRequest
    {
        [Required]
        public List<StudySessionRequest> Sessions { get; set; } = new();
    }

    public class FullSyncRequest
    {
        public List<CreateStreakRequest> Streaks { get; set; } = new();
        public List<ProgressSyncItem> Progress { get; set; } = new();
        public List<StudySessionRequest> Sessions { get; set; } = new();
        public string? Timezone { get; set; }
    }
}
