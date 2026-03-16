using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 Any authenticated user
    [Route("api/[controller]")]
    [ApiController]
    public class UserActivityController : ControllerBase
    {
    }
}
