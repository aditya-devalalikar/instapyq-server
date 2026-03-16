using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace pqy_server.Models.Content
{
    [Table("ContentPages")]
    public class ContentPage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? ContentHtml { get; set; }

        [Column(TypeName = "text")]
        public string? ContentJson { get; set; }

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? UpdatedByUserId { get; set; }
    }
}
