namespace pqy_server.Models.Auth
{
    public class GoogleLoginRequest
    {
        public required string IdToken { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(128)]
        public string? DeviceId { get; set; }
    }
}
