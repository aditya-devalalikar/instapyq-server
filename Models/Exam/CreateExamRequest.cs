using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Exams
{
    public class CreateExamRequest
    {
        [MaxLength(200)]
        public string? ExamName { get; set; }

        [Required]
        [MaxLength(20)]
        public string ShortName { get; set; } = null!;

        public int ExamOrder { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
