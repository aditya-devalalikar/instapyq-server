using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace pqy_server.Models.Images
{
    public class UploadImageRequest
    {
        [Required(ErrorMessage = "File is required.")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "QuestionId is required when uploading for a question.")]
        public int? QuestionId { get; set; }

        // Enum validation is automatic – no regex needed
        [Required]
        public ImageType Type { get; set; }
    }
}
