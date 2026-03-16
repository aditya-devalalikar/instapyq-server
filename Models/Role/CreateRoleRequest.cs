using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Roles
{
    public class CreateRoleRequest
    {
        [MaxLength(100)]
        public string? RoleName { get; set; }
    }
}
