using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Subjects
{
    public class CreateSubjectRequest
    {
        [MaxLength(200)]
        public string? SubjectName { get; set; }

        public int SubjectOrder { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
