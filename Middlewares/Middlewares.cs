using System.Text.Json;
using pqy_server.Shared;

namespace pqy_server.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                await WriteErrorResponseAsync(context);
            }
        }

        private static async Task WriteErrorResponseAsync(HttpContext context)
        {
            // IMPORTANT: do not write twice
            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var apiResponse = ApiResponse<string>.Failure(
                ResultCode.InternalServerError,
                "An unexpected error occurred. Please try again or contact support."
            );

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(apiResponse)
            );
        }
    }
}
