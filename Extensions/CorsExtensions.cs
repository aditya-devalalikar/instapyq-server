namespace pqy_server.Extensions
{
    public static class CorsExtensions
    {
        public static IServiceCollection AddAppCors(this IServiceCollection services, IConfiguration configuration)
        {
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? ["https://admin.instapyq.com", "https://dev.instapyq.com", "http://localhost:4200"];

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            return services;
        }
    }
}
