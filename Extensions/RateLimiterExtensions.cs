using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text.Json;
using pqy_server.Shared;
using Serilog;

namespace pqy_server.Extensions
{
    public static class RateLimiterExtensions
    {
        private static string GetClientIp(HttpContext context)
        {
            // RemoteIpAddress is set correctly by UseForwardedHeaders middleware
            // which trusts only known proxies (Railway). Do NOT read X-Forwarded-For
            // directly here — that would allow IP spoofing.
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    var ip = GetClientIp(context.HttpContext);
                    var path = context.HttpContext.Request.Path;
                    var userId = context.HttpContext.User?.FindFirst("sub")?.Value ?? "anonymous";

                    Log.Warning("Rate limit hit. ip={ip}, path={path}, uid={uid}", ip, path, userId);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    context.HttpContext.Response.Headers["Retry-After"] = "60";

                    var apiResponse = ApiResponse<string>.Failure(
                        ResultCode.TooManyRequests,
                        "Too many requests. Please try again in a minute."
                    );

                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        token);
                };

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var ip = GetClientIp(httpContext);
                    var path = httpContext.Request.Path.Value?.ToLower() ?? "";
                    var userId = httpContext.User?.FindFirst("sub")?.Value ?? "anonymous";
                    var key = $"{ip}-{userId}";

                    // Tier 1: Auth (Brute force protection)
                    if (path.Contains("/api/auth/"))
                    {
                        return RateLimitPartition.GetSlidingWindowLimiter(
                            $"auth-{key}",
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromMinutes(1),
                                SegmentsPerWindow = 6,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    }

                    // Tier 2: Expensive / PDF Operations (Anti-scraping)
                    if (path.Contains("/question-paper") || path.Contains("/answer-key") || path.Contains("/export"))
                    {
                        return RateLimitPartition.GetSlidingWindowLimiter(
                            $"expensive-{key}",
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = 10,
                                Window = TimeSpan.FromMinutes(1),
                                SegmentsPerWindow = 6,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    }

                    // Tier 3: General API
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        $"general-{key}",
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            });

            return services;
        }
    }
}
