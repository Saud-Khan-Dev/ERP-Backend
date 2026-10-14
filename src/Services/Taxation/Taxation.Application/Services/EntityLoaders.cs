using Microsoft.EntityFrameworkCore;

/// Load a tracked aggregate by id or throw the matching 404, so handlers read as one line each.
public static class EntityLoaders
{
  public static IQueryable<TaxFile> TaxFilesWithDetails(this IApplicationDbContext context) =>
      context.TaxFiles
        .Include(f => f.Bases)
        .Include(f => f.Payers)
        .Include(f => f.HeadValues)
        .Include(f => f.Overrides)
        .Include(f => f.Holds)
        .Include(f => f.Agreements).ThenInclude(a => a.Revisions)
        .AsSplitQuery();

  public static async Task<TaxFile> LoadTaxFileAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var fileId = TaxFileId.Of(id);
    return await context.TaxFilesWithDetails().FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken)
      ?? throw new TaxFileNotFoundException($"Tax file {id} was not found.");
  }

  public static async Task<TaxFile> LoadTaxFileByPsidAsync(this IApplicationDbContext context, string psid, CancellationToken cancellationToken)
  {
    var reference = new string((psid ?? "").Where(char.IsAsciiDigit).ToArray());
    return await context.TaxFilesWithDetails().FirstOrDefaultAsync(f => f.Psid == reference, cancellationToken)
      ?? throw new TaxFileNotFoundException($"No property has the reference {psid}.");
  }

  public static IQueryable<Demand> DemandsWithItems(this IApplicationDbContext context) =>
      context.Demands.Include(d => d.Lines).Include(d => d.ArrearsRows).Include(d => d.Penalties).AsSplitQuery();

  public static async Task<Demand> LoadDemandAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var demandId = DemandId.Of(id);
    return await context.DemandsWithItems().FirstOrDefaultAsync(d => d.Id == demandId, cancellationToken)
      ?? throw new DemandNotFoundException($"Demand {id} was not found.");
  }

  public static async Task<Notice> LoadNoticeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var noticeId = NoticeId.Of(id);
    return await context.Notices.FirstOrDefaultAsync(n => n.Id == noticeId, cancellationToken)
      ?? throw new NoticeNotFoundException($"Notice {id} was not found.");
  }

  public static async Task<TaxHead> LoadHeadAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var headId = TaxHeadId.Of(id);
    return await context.TaxHeads.FirstOrDefaultAsync(h => h.Id == headId, cancellationToken)
      ?? throw new TaxHeadNotFoundException($"Tax head {id} was not found.");
  }

  public static async Task<TaxHead> LoadHeadByCodeAsync(this IApplicationDbContext context, string code, CancellationToken cancellationToken)
  {
    var value = (code ?? "").Trim().ToUpperInvariant();
    return await context.TaxHeads.FirstOrDefaultAsync(h => h.Code == value, cancellationToken)
      ?? throw new TaxHeadNotFoundException($"There is no tax head {code}.");
  }

  public static async Task<RateSet> LoadRateSetAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var setId = RateSetId.Of(id);
    return await context.RateSets.Include(s => s.LtfFactors).Include(s => s.RateBands).AsSplitQuery().FirstOrDefaultAsync(s => s.Id == setId, cancellationToken)
      ?? throw new RateSetNotFoundException($"Rate set {id} was not found.");
  }

  public static async Task<TaxCircle> LoadCircleAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var circleId = TaxCircleId.Of(id);
    return await context.TaxCircles.Include(c => c.Towns).FirstOrDefaultAsync(c => c.Id == circleId, cancellationToken)
      ?? throw new TaxCircleNotFoundException($"Circle {id} was not found.");
  }

  public static async Task<SpecialPolicy> LoadPolicyAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var policyId = SpecialPolicyId.Of(id);
    return await context.SpecialPolicies.FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken)
      ?? throw new SpecialPolicyNotFoundException($"Special policy {id} was not found.");
  }

  public static async Task<PenaltyRule> LoadRuleAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var ruleId = PenaltyRuleId.Of(id);
    return await context.PenaltyRules.FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken)
      ?? throw new PenaltyRuleNotFoundException($"Penalty rule {id} was not found.");
  }

  public static async Task<DemandRun> LoadRunAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var runId = DemandRunId.Of(id);
    return await context.DemandRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
      ?? throw new DemandRunNotFoundException($"Demand run {id} was not found.");
  }

  public static async Task<Fee> LoadFeeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var feeId = FeeId.Of(id);
    return await context.Fees.FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken)
      ?? throw new FeeNotFoundException($"Fee {id} was not found.");
  }

  public static async Task<Party> LoadPartyAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var partyId = PartyId.Of(id);
    return await context.Parties.FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken)
      ?? throw new PartyNotFoundException($"Party {id} was not found.");
  }

  public static async Task<Clearance> LoadClearanceAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var clearanceId = ClearanceId.Of(id);
    return await context.Clearances.FirstOrDefaultAsync(c => c.Id == clearanceId, cancellationToken)
      ?? throw new ClearanceNotFoundException($"Clearance {id} was not found.");
  }

  /// The rows of a set that are in the database plus those added in this request and not saved yet.
  public static List<T> WithLocal<T>(this DbSet<T> set, IEnumerable<T> loaded, Func<T, bool> belongs) where T : class =>
      loaded.Union(set.Local.Where(belongs)).Distinct().ToList();
}
