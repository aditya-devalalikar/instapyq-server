using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Users
{
    public class UpdateUserRequest
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string? Username { get; set; }

        [MaxLength(256)]
        public string? Email { get; set; }

        [MaxLength(128)]
        public string? Password { get; set; }

        public List<int>? SelectedExamIds { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
