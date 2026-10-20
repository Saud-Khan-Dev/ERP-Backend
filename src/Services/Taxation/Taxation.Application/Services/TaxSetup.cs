using Microsoft.EntityFrameworkCore;

/// Reads the configuration in force: settings, heads, the rate set, special policies, penalty rules, templates, the
/// letterhead and the table layout. Scoped: each is loaded once per request.
public sealed class TaxSetup(IApplicationDbContext context)
{
  private readonly Dictionary<DateOnly, TaxSettings> _settings = new();
  private List<TaxHead>? _heads;
  private List<SpecialPolicy>? _policies;
  private readonly Dictionary<RateSetId, RateSet> _rateSets = new();
  private readonly Dictionary<TaxCircleId, TaxCircle> _circles = new();

  public async Task<TaxSettings> SettingsAsync(DateOnly asOf, CancellationToken cancellationToken)
  {
    if (_settings.TryGetValue(asOf, out var cached))
      return cached;
    var rows = await context.Settings.AsNoTracking().Where(s => s.EffectiveFrom <= asOf && (s.EffectiveTo == null || s.EffectiveTo >= asOf)).ToListAsync(cancellationToken);
    var values = rows.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFrom).First().Value);
    return _settings[asOf] = TaxSettings.From(values);
  }

  public async Task<IReadOnlyList<TaxHead>> HeadsAsync(CancellationToken cancellationToken) =>
      _heads ??= await context.TaxHeads.OrderBy(h => h.SortOrder).ThenBy(h => h.Name).ToListAsync(cancellationToken);

  public async Task<TaxHead> HeadAsync(string code, CancellationToken cancellationToken) =>
      (await HeadsAsync(cancellationToken)).FirstOrDefault(h => h.Code == code)
        ?? throw new TaxHeadNotFoundException($"The tax head {code} is missing. Restart the service to restore the seeded heads.");

  public async Task<IReadOnlyDictionary<TaxHeadId, string>> HeadCodesAsync(CancellationToken cancellationToken) =>
      (await HeadsAsync(cancellationToken)).ToDictionary(h => h.Id, h => h.Code);

  public async Task<IReadOnlyList<SpecialPolicy>> PoliciesAsync(CancellationToken cancellationToken) =>
      _policies ??= await context.SpecialPolicies.ToListAsync(cancellationToken);

  /// The approved rate set in force on a date (a superseded set still covers the dates before its successor).
  public async Task<RateSet> RateSetOnAsync(DateOnly date, CancellationToken cancellationToken)
  {
    var id = await context.RateSets.AsNoTracking()
      .Where(s => s.Status != RateSetStatus.Draft && s.EffectiveFrom <= date && (s.EffectiveTo == null || s.EffectiveTo >= date))
      .OrderByDescending(s => s.EffectiveFrom).Select(s => s.Id).FirstOrDefaultAsync(cancellationToken)
      ?? throw new DomainException($"No approved rate set is in force on {date:yyyy-MM-dd}. Approve one in Configuration → Rate sets.");
    if (_rateSets.TryGetValue(id, out var cached))
      return cached;
    return _rateSets[id] = await context.LoadRateSetAsync(id.Value, cancellationToken);
  }

  public async Task<PenaltyRule?> RuleAsync(PenaltyKind kind, DateOnly date, CancellationToken cancellationToken) =>
      await context.PenaltyRules.Where(r => r.Kind == kind && r.EffectiveFrom <= date && (r.EffectiveTo == null || r.EffectiveTo >= date))
        .OrderByDescending(r => r.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);

  public async Task<NoticeTemplate> TemplateAsync(NoticeStage stage, CancellationToken cancellationToken) =>
      await context.NoticeTemplates.FirstOrDefaultAsync(t => t.Stage == stage && t.IsActive, cancellationToken)
        ?? throw new DomainException($"There is no active {EnumText.Words(stage)} notice template. Set it in Configuration → Notices & SMS → Templates.");

  public async Task<NoticeLetterhead> LetterheadAsync(DateOnly date, CancellationToken cancellationToken) =>
      await context.Letterheads.Where(l => l.EffectiveFrom <= date && (l.EffectiveTo == null || l.EffectiveTo >= date))
        .OrderByDescending(l => l.EffectiveFrom).FirstOrDefaultAsync(cancellationToken)
        ?? await context.Letterheads.OrderBy(l => l.EffectiveFrom).FirstOrDefaultAsync(cancellationToken)
        ?? throw new DomainException("No notice letterhead is set. Set it in Configuration → Notices & SMS → Letterhead.");

  public async Task<NoticeTableLayout> LayoutAsync(DateOnly date, CancellationToken cancellationToken) =>
      await context.TableLayouts.Include(l => l.Rows).ThenInclude(r => r.Sources).Include(l => l.Columns).AsSplitQuery()
        .Where(l => l.EffectiveFrom <= date && (l.EffectiveTo == null || l.EffectiveTo >= date))
        .OrderByDescending(l => l.EffectiveFrom).FirstOrDefaultAsync(cancellationToken)
        ?? await context.TableLayouts.Include(l => l.Rows).ThenInclude(r => r.Sources).Include(l => l.Columns).AsSplitQuery()
          .OrderBy(l => l.EffectiveFrom).FirstOrDefaultAsync(cancellationToken)
        ?? throw new DomainException("No notice table layout is set. Set it in Configuration → Notices & SMS → Notice table.");

  public async Task<TaxCircle> CircleAsync(TaxCircleId id, CancellationToken cancellationToken)
  {
    if (_circles.TryGetValue(id, out var cached))
      return cached;
    return _circles[id] = await context.LoadCircleAsync(id.Value, cancellationToken);
  }

  /// The circle a town belongs to on a date.
  public async Task<TaxCircleTown?> CircleTownAsync(Guid townId, DateOnly date, CancellationToken cancellationToken) =>
      await context.CircleTowns.Where(t => t.TownId == townId && t.EffectiveFrom <= date && (t.EffectiveTo == null || t.EffectiveTo >= date))
        .FirstOrDefaultAsync(cancellationToken);

  /// The short code printed as "Town" on a notice (NG), else the town's code.
  public async Task<string> TownLabelAsync(TaxFile file, DateOnly date, CancellationToken cancellationToken)
  {
    var town = await CircleTownAsync(file.TownId, date, cancellationToken)
      ?? await context.CircleTowns.Where(t => t.TownId == file.TownId).OrderByDescending(t => t.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);
    return town?.ShortCode ?? town?.TownCode ?? "";
  }
}
