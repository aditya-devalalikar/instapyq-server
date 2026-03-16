namespace pqy_server.Models.Users
{
    public class ChangeUserRoleRequest
    {
        public int UserId { get; set; }
        public int NewRoleId { get; set; }
    }
}
