using Microsoft.EntityFrameworkCore;
using pqy_server.Models.Exams;
using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Years
{
    public partial class Year
    {
        [Key]
        public int YearId { get; set; }
        public string? YearName { get; set; }
        public string? PaperName { get; set; }

        [Required]
        public int ExamId { get; set; }

        [Required]
        public int YearOrder { get; set; }
        public Exam Exam { get; set; }
        public bool IsDeleted { get; set; } = false; // 👈 Soft delete flag
        public bool IsPremium { get; set; } = true; // New column, default to true

        public string? QuestionPaperKey { get; set; }
        public string? AnswerKeyKey { get; set; }
    }

}
