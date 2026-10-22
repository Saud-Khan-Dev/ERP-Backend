using Microsoft.EntityFrameworkCore;

/// Data scope by town (Administration › a person › Towns): a person whose access is limited to some towns sees and acts
/// only on the tax files of properties in those towns and on everything under them - demands, notices, surcharges,
/// payments, fees, clearances, opening arrears, overrides, holds, reference syncs and SMS. Anything outside is "not
/// found" (404), as if it did not exist. A fee or receipt of a registered party (no tax file) is not in a town, so it is
/// seen by everyone who may see fees. Configuration (circles, heads, rates) is not scoped.
///
/// The bulk acts that reach every file (the daily job, the year-end surcharge, preparing due notices) need every town;
/// a demand run or a surcharge run needs every town its circle (or all circles) covers.
public static class TaxScope
{
  public static bool IsLimited(this IApplicationDbContext context) => context.TownScope is not null;

  public static bool Covers(this IApplicationDbContext context, Guid townId) => context.TownScope is not { } towns || towns.Contains(townId);

  public static bool Covers(this IApplicationDbContext context, TaxFile file) => context.Covers(file.TownId);

  /// The tax files the person may see.
  public static IQueryable<TaxFile> ScopedFiles(this IApplicationDbContext context) =>
      context.TownScope is { } towns ? context.TaxFiles.Where(f => towns.Contains(f.TownId)) : context.TaxFiles;

  private static IQueryable<TaxFileId> FileIds(IApplicationDbContext context) => context.ScopedFiles().Select(f => f.Id);

  public static IQueryable<Demand> InScope(this IQueryable<Demand> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(d => ids.Contains(d.TaxFileId));
  }

  public static IQueryable<Notice> InScope(this IQueryable<Notice> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(n => ids.Contains(n.TaxFileId));
  }

  public static IQueryable<Penalty> InScope(this IQueryable<Penalty> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(p => ids.Contains(p.TaxFileId));
  }

  public static IQueryable<Arrears> InScope(this IQueryable<Arrears> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(a => ids.Contains(a.TaxFileId));
  }

  public static IQueryable<Clearance> InScope(this IQueryable<Clearance> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(c => ids.Contains(c.TaxFileId));
  }

  public static IQueryable<TaxFileOverride> InScope(this IQueryable<TaxFileOverride> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(o => ids.Contains(o.TaxFileId));
  }

  public static IQueryable<LitigationHold> InScope(this IQueryable<LitigationHold> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(h => ids.Contains(h.TaxFileId));
  }

  public static IQueryable<PsidSync> InScope(this IQueryable<PsidSync> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(s => ids.Contains(s.TaxFileId));
  }

  /// A party's fee receipt (no file) stays visible.
  public static IQueryable<Payment> InScope(this IQueryable<Payment> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(p => p.TaxFileId == null || ids.Contains(p.TaxFileId));
  }

  /// A party's fee (no file) stays visible.
  public static IQueryable<Fee> InScope(this IQueryable<Fee> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(f => f.TaxFileId == null || ids.Contains(f.TaxFileId));
  }

  public static IQueryable<SmsMessage> InScope(this IQueryable<SmsMessage> rows, IApplicationDbContext context)
  {
    if (!context.IsLimited())
      return rows;
    var ids = FileIds(context);
    return rows.Where(m => m.TaxFileId == null || ids.Contains(m.TaxFileId));
  }

  /// Whether a run of this circle (null = every circle) is shown to the person: every circle's runs, and those of the
  /// circles holding one of their towns (the run lists only the person's own files).
  public static async Task<bool> SeesRunOfAsync(this IApplicationDbContext context, TaxCircleId? circleId, CancellationToken cancellationToken) =>
      context.TownScope is not { } towns || circleId is null
      || await context.CircleTowns.AnyAsync(t => t.CircleId == circleId && towns.Contains(t.TownId), cancellationToken);

  /// The refusal for a bulk act that reaches every file, or null when the person works on every town.
  public static string? EveryTownRefusal(this IApplicationDbContext context, string act) => context.IsLimited()
    ? $"You cannot {act}: it reaches the tax files of every town, and your access is limited to some towns. Someone who works on every town does it."
    : null;

  /// The refusal for a run over one circle (or all circles), or null when the person works on every town it reaches.
  public static async Task<string?> CircleRefusalAsync(this IApplicationDbContext context, TaxCircleId? circleId, string act, CancellationToken cancellationToken)
  {
    if (context.TownScope is not { } towns)
      return null;
    if (circleId is null)
      return $"You cannot {act} for every circle: your access is limited to some towns. Choose a circle whose towns are all yours, or ask someone who works on every town.";
    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    var circleTowns = await context.CircleTowns.AsNoTracking().Where(t => t.CircleId == circleId && (t.EffectiveTo == null || t.EffectiveTo >= today))
      .Select(t => new { t.TownId, t.TownCode })
      .ToListAsync(cancellationToken);
    var outside = circleTowns.Where(t => !towns.Contains(t.TownId)).Select(t => t.TownCode).Distinct().ToList();
    return outside.Count == 0 ? null
      : $"You cannot {act} for this circle: it also covers {string.Join(", ", outside)}, outside the towns you work on. Someone who works on all of its towns does it.";
  }
}
