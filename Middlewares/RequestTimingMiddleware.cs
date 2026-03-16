using Serilog;
using System.Diagnostics;

namespace pqy_server.Middlewares
{
    public class RequestTimingMiddleware
    {
        private readonly RequestDelegate _next;

        // Threshold: requests taking longer than this are logged as warnings
        private const int SlowRequestMs = 2000;

        public RequestTimingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;

                // Only log slow requests to keep volume low
                if (elapsedMs >= SlowRequestMs)
                {
                    Log.Warning(
                        "Slow request. {mtd} {path} {ms}ms sc={sc}",
                        context.Request.Method,
                        context.Request.Path,
                        elapsedMs,
                        statusCode
                    );
                }
            }
        }
    }
}
