using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.QuoteRequests
{
    public class QuoteCreateRequest
    {
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Author { get; set; }

        // Optional image file
        public IFormFile? File { get; set; }
    }

    public class QuoteUpdateRequest
    {
        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Author { get; set; }

        // Optional image file to replace existing one
        public IFormFile? File { get; set; }
    }
}
