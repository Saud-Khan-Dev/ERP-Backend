using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// Fills empty master tables with the schema's starting values and creates the code sequences.
/// Idempotent and safe on every start: it only ever adds to an empty table or a missing sequence.
public sealed class PropertySeeder(ApplicationDbContext context, ILogger<PropertySeeder> logger)
{
  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    foreach (var descriptor in MasterRegistry.All)
    {
      if (!MasterDataSeed.BySlug.TryGetValue(descriptor.Slug, out var values))
        continue;

      if ((await descriptor.ListAsync(context, true, cancellationToken)).Count > 0)
        continue;

      var order = 0;
      foreach (var value in values)
      {
        order += 10;
        var master = descriptor.Create(MasterId.New(), MasterCode.Of(value.Code), Name.Of(value.Name, MasterData.NameMaxLength),
          null, order, value.Extras ?? MasterExtras.None);

        await descriptor.AddAsync(context, master, cancellationToken);
      }

      logger.LogInformation("Seeded {Count} {Master} value(s).", values.Length, descriptor.Table);
    }

    foreach (var defaults in CodeSequenceKeys.Defaults)
    {
      var key = MasterCode.Of(defaults.Key);
      if (await context.CodeSequences.AnyAsync(s => s.Key == key, cancellationToken))
        continue;

      await context.CodeSequences.AddAsync(
        CodeSequence.Create(CodeSequenceId.New(), key, defaults.Prefix, defaults.Separator, defaults.MinimumDigits, 1), cancellationToken);

      logger.LogInformation("Seeded code sequence {Key} ({Prefix}{Separator}...).", defaults.Key, defaults.Prefix, defaults.Separator);
    }

    await context.SaveChangesAsync(cancellationToken);
  }
}
