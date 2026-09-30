using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// Brings a new database to a usable state at startup: wait for the server, apply migrations, seed.
public sealed class DatabaseInitializer(ApplicationDbContext context, PropertySeeder seeder, ILogger<DatabaseInitializer> logger)
{
  public async Task InitialiseAsync(bool applyMigrations, CancellationToken cancellationToken = default)
  {
    // docker compose starts the containers together, so Postgres may not accept connections yet
    await WaitForDatabaseAsync(cancellationToken);

    if (applyMigrations)
    {
      var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

      if (pending.Count > 0)
      {
        logger.LogInformation("Applying {Count} pending migration(s) to the {Schema} schema.", pending.Count, ApplicationDbContext.Schema);
        await context.Database.MigrateAsync(cancellationToken);
      }
    }

    await seeder.SeedAsync(cancellationToken);
  }

  private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
  {
    const int maxAttempts = 12;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
      try
      {
        if (await context.Database.CanConnectAsync(cancellationToken))
          return;
      }
      catch (Exception ex) when (attempt < maxAttempts)
      {
        logger.LogInformation("Database not reachable yet ({Message}); retrying {Attempt}/{Max}.", ex.Message, attempt, maxAttempts);
      }

      if (attempt == maxAttempts)
        throw new InvalidOperationException(
          $"The database was not reachable after {maxAttempts} attempts. Is PostgreSQL running (docker compose up -d postgres)?");

      await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
  }
}
