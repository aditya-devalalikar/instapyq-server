using pqy_server.Models.Order;
using pqy_server.Services;
using pqy_server.Services.EmailService;

namespace pqy_server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<FcmNotificationService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.Configure<RazorpaySettings>(configuration.GetSection("Razorpay"));
            services.AddScoped<ILogService, LogService>();
            services.AddScoped<ILeaderboardService, LeaderboardService>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<ReorderService>();
            services.AddSingleton<MediaUrlBuilder>();
            services.AddHttpClient<IEmailService, EmailService>();
            services.AddScoped<IRazorpayService, RazorpayService>();

            // AI Services
            services.Configure<pqy_server.Services.AiService.AiConfigurationSettings>(configuration.GetSection("AiConfiguration"));
            services.AddHttpClient<pqy_server.Services.AiService.IAiProviderService, pqy_server.Services.AiService.Providers.GeminiProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10); // Batch file polling can take several minutes for large payloads
            });
            services.AddHttpClient<pqy_server.Services.AiService.IAiProviderService, pqy_server.Services.AiService.Providers.OpenAiProviderService>();
            services.AddHttpClient<pqy_server.Services.AiService.IAiProviderService, pqy_server.Services.AiService.Providers.ClaudeProviderService>();
            services.AddScoped<pqy_server.Services.AiService.AiProviderFactory>();

            return services;
        }
    }
}
