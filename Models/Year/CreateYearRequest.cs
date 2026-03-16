using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Years
{
    public class CreateYearRequest
    {
        [MaxLength(50)]
        public string? YearName { get; set; }

        [MaxLength(200)]
        public string? PaperName { get; set; }

        public int ExamId { get; set; }
        public bool? IsDeleted { get; set; }
    }
}
