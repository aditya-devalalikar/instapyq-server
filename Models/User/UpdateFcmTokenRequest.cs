using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Users
{
    public class UpdateFcmTokenRequest
    {
        [Required]
        [MaxLength(500)]
        public string FcmToken { get; set; } = string.Empty;

        /// <summary>IANA timezone string e.g. "Asia/Kolkata". Optional — sent by client on every token refresh.</summary>
        [MaxLength(100)]
        public string? Timezone { get; set; }
    }
}
