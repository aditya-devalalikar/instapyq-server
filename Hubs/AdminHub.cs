using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using pqy_server.Constants;

namespace pqy_server.Hubs
{
    [Authorize(Roles = RoleConstant.Admin)]
    public class AdminHub : Hub
    {
    }
}
