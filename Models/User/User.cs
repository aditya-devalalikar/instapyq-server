using pqy_server.Models.Roles;

namespace pqy_server.Models.Users
{
    public class User
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? UserEmail { get; set; }

        // Password fields are now nullable for Google-only users
        public byte[]? PasswordHash { get; set; }
        public byte[]? PasswordSalt { get; set; }

        // Google login fields
        public string? GoogleId { get; set; }                // Stores Google account ID (sub)
        public string? GoogleProfilePicture { get; set; }    // Optional avatar URL
        public bool IsGoogleLoginOnly { get; set; } = false;

        public int RoleId { get; set; }
        public Role Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public string? DeviceId { get; set; }

        public List<int> SelectedExamIds { get; set; } = new();

        public bool IsDeleted { get; set; } = false; // 👈 Soft delete flag
        public string? FcmToken { get; set; }
        public bool HideFromLeaderboard { get; set; } = false;
    }
}
