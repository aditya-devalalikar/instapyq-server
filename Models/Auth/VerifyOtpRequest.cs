using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Auth
{
    public class VerifyOtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? DeviceId { get; set; }
    }
}
