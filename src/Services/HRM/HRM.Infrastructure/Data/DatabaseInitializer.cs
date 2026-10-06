using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

/// Brings a new database to a usable state at startup: wait for the server, apply migrations, seed.
public sealed class DatabaseInitializer(ApplicationDbContext context, HrmSeeder seeder, ILogger<DatabaseInitializer> logger)
{
  public async Task InitialiseAsync(bool applyMigrations, CancellationToken cancellationToken = default)
  {
    // docker compose starts the containers together, so Postgres may not accept connections yet
    await WaitForServerAsync(applyMigrations, cancellationToken);

    if (applyMigrations)
    {
      var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

      if (pending.Count > 0)
      {
        logger.LogInformation("Applying {Count} pending migration(s) to the {Schema} schema.", pending.Count, ApplicationDbContext.Schema);
        await context.Database.MigrateAsync(cancellationToken);

        // Npgsql caches the database's types when it first connects; the enum types were only just created
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
          await ((Npgsql.NpgsqlConnection)context.Database.GetDbConnection()).ReloadTypesAsync(cancellationToken);
        }
        finally
        {
          await context.Database.CloseConnectionAsync();
        }
      }
    }

    await seeder.SeedAsync(cancellationToken);
  }

  /// Waits until the server answers. A database that does not exist yet is fine when migrations run: migrating
  /// creates it.
  private async Task WaitForServerAsync(bool applyMigrations, CancellationToken cancellationToken)
  {
    const int maxAttempts = 12;
    var creator = context.GetService<IRelationalDatabaseCreator>();

    for (var attempt = 1; ; attempt++)
    {
      try
      {
        if (await creator.ExistsAsync(cancellationToken))
          return;
        if (applyMigrations)
        {
          logger.LogInformation("The database does not exist yet; migrating will create it.");
          return;
        }
        throw new InvalidOperationException("The database does not exist and Database:AutoMigrate is off. Create it or turn migrations on.");
      }
      catch (Exception ex) when (ex is not InvalidOperationException && attempt < maxAttempts)
      {
        logger.LogInformation("Database server not reachable yet ({Message}); retrying {Attempt}/{Max}.", ex.Message, attempt, maxAttempts);
      }
      catch (Exception ex) when (ex is not InvalidOperationException)
      {
        throw new InvalidOperationException(
          $"The database server was not reachable after {maxAttempts} attempts. Is PostgreSQL running (docker compose up -d postgres)?", ex);
      }

      await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
  }
}
