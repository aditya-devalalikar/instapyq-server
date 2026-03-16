using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Users
{
    public class CreateUserRequest
    {
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string UserEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public List<int> SelectedExamIds { get; set; } = new();
        public bool IsDeleted { get; set; } = false;
    }
}
