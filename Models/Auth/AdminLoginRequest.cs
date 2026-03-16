using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Auth
{
    public class AdminLoginRequest
    {
        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;
    }
}
