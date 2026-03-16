using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Exams
{
    public partial class Exam
    {
        [Key]
        public int ExamId { get; set; }

        [Required]
        public string ExamName { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string ShortName { get; set; } = null!;

        [Required]
        [Range(1, int.MaxValue)]
        public int ExamOrder {  get; set; }

        public bool IsDeleted { get; set; } = false; // 👈 Soft delete flag
    }

}
