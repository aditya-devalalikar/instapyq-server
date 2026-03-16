using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Auth
{
    public class SendOtpRequest
    {
        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
