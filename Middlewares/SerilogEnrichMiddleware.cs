using Serilog.Context;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace pqy_server.Middlewares
{
    public class SerilogEnrichMiddleware
    {
        private const string CorrelationHeader = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public SerilogEnrichMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Correlation ID (from header or new GUID)
            var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N"); // no hyphens = shorter
            }
            context.Response.Headers[CorrelationHeader] = correlationId;

            // Extract user info from JWT claims
            var userId = context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var role = context.User?.FindFirstValue(ClaimTypes.Role);
            var isPremium = context.User?.FindFirstValue("isPremium") == "true";

            // Short property names for storage efficiency
            using (LogContext.PushProperty("cid", correlationId))
            using (LogContext.PushProperty("uid", userId))
            using (LogContext.PushProperty("role", role))
            using (LogContext.PushProperty("prm", isPremium))
            using (LogContext.PushProperty("path", context.Request.Path.ToString()))
            using (LogContext.PushProperty("mtd", context.Request.Method))
            {
                await _next(context);
            }
        }
    }
}
