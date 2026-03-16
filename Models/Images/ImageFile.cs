using System;
using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Images
{
    public class ImageFile
    {
        [Key]
        public int ImageId { get; set; }

        [Required]
        public string FileName { get; set; } = null!;

        [Required]
        public string BucketKey { get; set; } = null!;

        [Required]
        public string EntityType { get; set; } = null!; // e.g., "Question", "Quote", etc.

        [Required]
        public int EntityId { get; set; } // Related entity's ID

        public ImageType ImageType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public enum ImageType
    {
        Question,
        OptionA,
        OptionB,
        OptionC,
        OptionD,
        Answer
    }
}
