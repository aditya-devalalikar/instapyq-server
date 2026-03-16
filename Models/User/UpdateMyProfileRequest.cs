using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Users
{
    public class UpdateMyProfileRequest
    {
        [MaxLength(100)]
        public string? Username { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }

        public List<int>? SelectedExamIds { get; set; }
        public bool? HideFromLeaderboard { get; set; }
    }
}
