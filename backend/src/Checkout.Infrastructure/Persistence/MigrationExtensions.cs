using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Checkout.Infrastructure.Persistence;

public static class MigrationExtensions
{
    private const int MaxAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>Applies pending migrations (schema + seed), retrying while MySQL is still starting.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider sp, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = sp.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CheckoutDbContext>();
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database migrated on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{MaxAttempts}): {Error}. Retrying in {Delay}s",
                    attempt, MaxAttempts, ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
