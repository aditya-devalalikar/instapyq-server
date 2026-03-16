using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Years
{
    public class UploadYearPdfRequest
    {
        [Required(ErrorMessage = "File is required.")]
        public IFormFile File { get; set; } = null!;
    }
}
