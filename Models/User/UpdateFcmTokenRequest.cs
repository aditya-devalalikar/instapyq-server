using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Users
{
    public class UpdateFcmTokenRequest
    {
        [Required]
        [MaxLength(500)]
        public string FcmToken { get; set; } = string.Empty;
    }
}
