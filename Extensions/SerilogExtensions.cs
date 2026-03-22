using Serilog;

namespace pqy_server.Extensions
{
    public static class SerilogExtensions
    {
        public static WebApplicationBuilder AddAppSerilog(
            this WebApplicationBuilder builder)
        {
            var seqUrl = builder.Configuration["Seq:ServerUrl"];
            var apiKey = builder.Configuration["Seq:ApiKey"];

            var config = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                // Wrap the console sink in an async sink with a bounded buffer of 1000
                // log events. Without async, each log statement blocks the request thread
                // while waiting for stdout I/O to flush — under log bursts this adds
                // latency and causes threads to pile up. The async wrapper offloads writes
                // to a background thread; if the buffer fills up, excess events are dropped
                // (dropOnQueueFull: true) rather than blocking the caller.
                // blockWhenFull: false (default) = drop excess events instead of
                // blocking the caller when the buffer fills up during a log burst.
                .WriteTo.Async(a => a.Console(), bufferSize: 1000, blockWhenFull: false);

            // Seq sink — only when a URL is configured
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                config = config.WriteTo.Async(a => a.Seq(seqUrl, apiKey: apiKey),
                    bufferSize: 1000, blockWhenFull: false);
            }

            Log.Logger = config.CreateLogger();

            builder.Host.UseSerilog();

            return builder;
        }
    }
}
