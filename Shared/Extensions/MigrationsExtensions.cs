using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Extensions;

public static class MigrationsExtensions
{
    extension(IApplicationBuilder app)
    {
        public void ApplyMigrations<T>()
            where T : DbContext
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<T>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<T>>();

            try
            {
                var pendingMigrations = context.Database.GetPendingMigrations().ToList();

                if (pendingMigrations.Count != 0)
                {
                    context.Database.Migrate();
                    logger.LogInformation("Migrations applied successfully for {Context}.", typeof(T).Name);
                }
                else
                {
                    logger.LogInformation("No pending migrations for {Context}.", typeof(T).Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying migrations for {Context}.", typeof(T).Name);
                throw;
            }
        }
    }
}