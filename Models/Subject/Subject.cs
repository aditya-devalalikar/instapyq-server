using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Subjects
{
    public partial class Subject
    {
        [Key]
        public int SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public int SubjectOrder {  get; set; }
        public bool IsDeleted { get; set; } = false; // 👈 Soft delete flag
    }

}
