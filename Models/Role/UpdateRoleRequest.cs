using System.ComponentModel.DataAnnotations;

namespace pqy_server.Models.Roles
{
    public class UpdateRoleRequest
    {
        [MaxLength(100)]
        public string? RoleName { get; set; }
    }
}
