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

                // Maximum Pool Size=16 aligns Npgsql's physical connection pool with
                // the EF DbContextPool size (also 16). Without this Npgsql defaults to
                // 100 physical connections — far more than EF ever needs, wasting
                // memory on idle Postgres backend processes.
                connectionString =
                    $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.TrimStart('/')};" +
                    "Maximum Pool Size=16;Minimum Pool Size=1;Connection Idle Lifetime=60;Connection Pruning Interval=10;" +
                    (isLocal
                        ? "SSL Mode=Disable;"
                        : "SSL Mode=Require;Trust Server Certificate=true;");
            }
            else
            {
                connectionString = databaseUrl.Contains("localhost")
                    ? databaseUrl + ";Maximum Pool Size=16;Minimum Pool Size=1;Connection Idle Lifetime=60;Connection Pruning Interval=10;SSL Mode=Disable;"
                    : databaseUrl + ";Maximum Pool Size=16;Minimum Pool Size=1;Connection Idle Lifetime=60;Connection Pruning Interval=10;SSL Mode=Require;Trust Server Certificate=true;";
            }

            // Pool size of 16: comfortably handles ~50 concurrent requests
            // (not all requests hit the DB simultaneously — cached responses,
            // auth checks, and validation short-circuit before DB access).
            // Default of 1024 is wasteful; 16 fits well within Railway's
            // Postgres plan connection limit (~25–100 depending on tier).
            services.AddDbContextPool<AppDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    npgsqlOptions.CommandTimeout(30);
                }), poolSize: 16);

            return services;
        }
    }
}

