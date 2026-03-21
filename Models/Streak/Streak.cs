using System.ComponentModel.DataAnnotations;
using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Streak
{
    // ─── Entity ─────────────────────────────────────────────────────────────────

    public class Streak
    {
        public int StreakId { get; set; }

        public int UserId { get; set; }
        public UserEntity User { get; set; } = null!;

        /// <summary>Client-generated ID (MMKV key) used for deduplication on sync.</summary>
        public string ClientId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        /// <summary>'daily' | 'specific_days'</summary>
        public string Frequency { get; set; } = "daily";

        /// <summary>JSON array of day numbers e.g. [1,3,5]. Null when frequency = 'daily'.</summary>
        public string? SpecificDays { get; set; }

        public string? Category { get; set; }
        public bool IsTimer { get; set; } = false;

        /// <summary>Daily study target in minutes. Only set when IsTimer = true.</summary>
        public int? TargetMinutes { get; set; }

        /// <summary>JSON array of alert objects e.g. [{hour,minute,label}].</summary>
        public string? Alerts { get; set; }

        /// <summary>Cached current streak length in days. Updated on every progress sync.</summary>
        public int CurrentStreakDays { get; set; } = 0;

        /// <summary>Cached all-time longest streak. Updated on every progress sync.</summary>
        public int LongestStreakDays { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class StreakDto
    {
        public int StreakId { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public List<int>? SpecificDays { get; set; }
        public string? Category { get; set; }
        public bool IsTimer { get; set; }
        public int? TargetMinutes { get; set; }
        public List<AlertDto>? Alerts { get; set; }
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AlertDto
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string? Label { get; set; }
    }

    // ─── Requests ────────────────────────────────────────────────────────────────

    public class CreateStreakRequest
    {
        [Required]
        [MaxLength(50)]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(10)]
        public string Color { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Icon { get; set; } = string.Empty;

        [Required]
        public string Frequency { get; set; } = "daily";

        public List<int>? SpecificDays { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        public bool IsTimer { get; set; } = false;
        public int? TargetMinutes { get; set; }
        public List<AlertDto>? Alerts { get; set; }
    }

    public class UpdateStreakRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        [MaxLength(10)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? Icon { get; set; }

        public string? Frequency { get; set; }
        public List<int>? SpecificDays { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        public bool? IsTimer { get; set; }
        public int? TargetMinutes { get; set; }
        public List<AlertDto>? Alerts { get; set; }
    }
}
