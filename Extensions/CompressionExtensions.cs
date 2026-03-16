using Microsoft.AspNetCore.ResponseCompression;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace pqy_server.Extensions
{
    public static class CompressionExtensions
    {
        public static IServiceCollection AddAppCompression(this IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;

                options.Providers.Add<GzipCompressionProvider>();
                options.Providers.Add<BrotliCompressionProvider>();

                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/json" });
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            return services;
        }
    }
}
