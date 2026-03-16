using Microsoft.EntityFrameworkCore;
using pqy_server.Data;

namespace pqy_server.Extensions
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddAppDatabase(
            this IServiceCollection services)
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                              ?? "Host=localhost;Port=5432;Database=PYQ_Dev;Username=postgres;Password=admin123";

            if (string.IsNullOrEmpty(databaseUrl))
                throw new Exception("DATABASE_URL not configured.");

            string connectionString;

            if (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://"))
            {
                var uri = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':');

                bool isLocal = uri.Host.Contains("localhost") || uri.Host.Contains("127.0.0.1");

                connectionString =
                    $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.TrimStart('/')};" +
                    (isLocal
                        ? "SSL Mode=Disable;"
                        : "SSL Mode=Require;Trust Server Certificate=true;");
            }
            else
            {
                connectionString = databaseUrl.Contains("localhost")
                    ? databaseUrl + ";SSL Mode=Disable;"
                    : databaseUrl + ";SSL Mode=Require;Trust Server Certificate=true;";
            }

            services.AddDbContextPool<AppDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    npgsqlOptions.CommandTimeout(30);
                }));

            return services;
        }
    }
}

