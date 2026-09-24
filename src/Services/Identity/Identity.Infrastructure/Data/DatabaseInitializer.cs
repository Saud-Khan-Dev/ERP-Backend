using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// Brings a brand-new database up to a usable state at startup: wait for the server, apply
/// migrations, then seed.
///
/// This is what makes "clone the repo and run it" work — without it, a new developer has to run
/// `dotnet ef database update` by hand before the service will start.
public sealed class DatabaseInitializer(
    ApplicationDbContext context,
    IdentitySeeder seeder,
    ILogger<DatabaseInitializer> logger)
{
  public async Task InitialiseAsync(bool applyMigrations, CancellationToken cancellationToken = default)
  {
    // docker compose starts the containers together, so Postgres is often not accepting
    // connections yet when this runs
    await WaitForDatabaseAsync(cancellationToken);

    if (applyMigrations)
    {
      var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

      if (pending.Count > 0)
      {
        logger.LogInformation("Applying {Count} pending migration(s) to the auth schema.", pending.Count);
        await context.Database.MigrateAsync(cancellationToken);
      }
    }

    await seeder.SeedAsync(cancellationToken);
  }

  private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
  {
    const int maxAttempts = 12;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
      try
      {
        if (await context.Database.CanConnectAsync(cancellationToken))
          return;
      }
      catch (Exception ex) when (attempt < maxAttempts)
      {
        logger.LogInformation("Database not reachable yet ({Message}); retrying {Attempt}/{Max}.",
          ex.Message, attempt, maxAttempts);
      }

      if (attempt == maxAttempts)
        throw new InvalidOperationException(
          $"The database was not reachable after {maxAttempts} attempts. Is PostgreSQL running (docker compose up -d postgres)?");

      await Task.Delay(delay, cancellationToken);
    }
  }
}
