using pqy_server.Hubs;
using pqy_server.Middlewares;

namespace pqy_server.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static WebApplication UseAppPipeline(this WebApplication app)
        {
            // Critical #1 fix: must come before rate limiter so RemoteIpAddress is
            // set from the trusted proxy before IP-based rate limiting runs
            app.UseForwardedHeaders();
            app.UseOutputCache();
            app.UseResponseCompression();
            app.UseRateLimiter();
            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();

            // Structured logging: enrich every request with user/correlation context
            app.UseMiddleware<SerilogEnrichMiddleware>();
            // Performance: log slow requests (>2s)
            app.UseMiddleware<RequestTimingMiddleware>();
            // Global error handling (after enrichment so it captures uid/cid)
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Security headers for all responses
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
                await next();
            });

            app.MapControllers();
            app.MapHub<AdminHub>("/hubs/admin");

            return app;
        }
    }
}
