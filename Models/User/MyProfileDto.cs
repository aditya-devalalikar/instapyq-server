namespace pqy_server.Models.User
{
    public class MyProfileDto
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        public string Role { get; set; } = "Unknown";

        public bool IsPremium { get; set; }

        public DateTime CreatedAt { get; set; }

        // Store multiple exam selections
        public List<int> SelectedExamIds { get; set; } = new();
        public bool HideFromLeaderboard { get; set; }
        public bool IsGoogleLoginOnly { get; set; }
    }
}
