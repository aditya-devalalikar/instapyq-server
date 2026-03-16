using Microsoft.AspNetCore.Mvc;

namespace pqy_server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        // ✅ GET: /api/health
        // Returns a standardized health check response
        [HttpGet("")]
        public IActionResult HealthCheck()
        {
            // Use your shared ApiResponse<T> to wrap the success message
            return Ok(pqy_server.Shared.ApiResponse<string>.Success("Application running OK!!"));
        }
    }
}
