namespace pqy_server.Models.Roles
{
    public class Role
    {
        public int RoleId { get; set; }         // 1 = Admin, 2 = User, etc.
        public string? RoleName { get; set; }    // "Admin", "User", etc.
    }
}
