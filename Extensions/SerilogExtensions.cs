using Serilog;

namespace pqy_server.Extensions
{
    public static class SerilogExtensions
    {
        public static WebApplicationBuilder AddAppSerilog(
            this WebApplicationBuilder builder)
        {
            var config = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(); // Railway logs (always on)

            // Seq sink — only when a URL is configured
            var seqUrl = builder.Configuration["Seq:ServerUrl"];
            var apiKey = builder.Configuration["Seq:ApiKey"];

            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                config = config.WriteTo.Seq(seqUrl, apiKey: apiKey);
            }

            Log.Logger = config.CreateLogger();

            builder.Host.UseSerilog();

            return builder;
        }
    }
}
