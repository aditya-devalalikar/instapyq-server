using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace pqy_server.Models.Quotes
{
    [Table("Quotes")]
    public class Quote
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Author { get; set; }

        // Optional image (URL or base64)
        [MaxLength(1000)]
        public string? ImageUrl { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
