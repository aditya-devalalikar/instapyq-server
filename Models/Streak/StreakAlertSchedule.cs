using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Streak
{
    public class StreakAlertSchedule
    {
        public long Id { get; set; }

        public int StreakId { get; set; }
        public Streak Streak { get; set; } = null!;

        public int UserId { get; set; }
        public UserEntity User { get; set; } = null!;

        public string Timezone { get; set; } = string.Empty;
        public int LocalHour { get; set; }
        public int LocalMinute { get; set; }
        public string? Label { get; set; }

        public DateTime NextFireUtc { get; set; }
        public DateTime? LeaseUntilUtc { get; set; }
        public DateTime? LastSentUtc { get; set; }
        public DateOnly? LastSentLocalDate { get; set; }

        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
