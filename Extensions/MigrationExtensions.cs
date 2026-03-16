using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Initializers;

namespace pqy_server.Extensions
{
    public static class MigrationExtensions
    {
        public static void ApplyMigrations(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.Database.Migrate();
            SeedEnumLabels.Seed(context);
        }
    }
}
