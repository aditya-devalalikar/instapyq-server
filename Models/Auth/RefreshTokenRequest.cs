using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        [MaxLength(512)]
        public string RefreshToken { get; set; }

        [MaxLength(128)]
        public string? DeviceId { get; set; }
    }
}
