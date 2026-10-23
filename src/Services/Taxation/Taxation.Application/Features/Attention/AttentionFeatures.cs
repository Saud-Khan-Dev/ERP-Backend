using Microsoft.EntityFrameworkCore;

/// The queues of Taxation work waiting for the caller (the bell and the overview's "Needs attention"): only the queues the
/// caller may act on, each with an exact count (zero = checked, nothing waits). Keys are listed in docs/claude/taxation.md.
public sealed record GetAttentionQuery : IQuery<Result<AttentionResponse>>;

public class AttentionHandlers(IApplicationDbContext context, TaxSetup setup, DueNotices dueNotices, ICurrentUser currentUser, IClock clock) :
  IQueryHandler<GetAttentionQuery, Result<AttentionResponse>>
{
  public async Task<Result<AttentionResponse>> Handle(GetAttentionQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var fy = FiscalYear.Of(today);
    var items = new List<AttentionItemDto>();
    bool Can(string permission) => currentUser.HasPermission(permission);
    static DateTimeOffset? At(DateTime? utc) => utc is { } t ? new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)) : null;
    static DateTimeOffset? Day(DateOnly? day) => day is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)) : null;

    // ---- demand runs ----
    if (Can(PermissionCatalog.Tax.Approve) || Can(PermissionCatalog.Tax.Create))
    {
      var runs = context.DemandRuns.AsNoTracking().Where(r => r.Status == DemandRunStatus.Computed);
      if (context.TownScope is { } towns)
      {
        // the runs the person can approve: of circles all of whose towns are theirs
        var circles = await context.CircleTowns.AsNoTracking().Where(t => t.EffectiveTo == null || t.EffectiveTo >= today).Select(t => new { t.CircleId, t.TownId })
          .ToListAsync(cancellationToken);
        var theirs = circles.GroupBy(t => t.CircleId).Where(g => g.All(t => towns.Contains(t.TownId))).Select(g => g.Key).ToList();
        runs = runs.Where(r => r.CircleId != null && theirs.Contains(r.CircleId));
      }
      var computed = await runs
        .Select(r => new { r.ComputedAt, r.ExceptionCount }).ToListAsync(cancellationToken);
      if (Can(PermissionCatalog.Tax.Approve))
        items.Add(new("runs.computed", computed.Count, At(computed.Min(r => r.ComputedAt))));
      if (Can(PermissionCatalog.Tax.Create))
        items.Add(new("runs.problems", computed.Sum(r => r.ExceptionCount ?? 0), At(computed.Where(r => r.ExceptionCount > 0).Min(r => r.ComputedAt))));
    }

    // ---- notices and demands ----
    if (Can(PermissionCatalog.Tax.Edit))
    {
      var pending = await context.Notices.InScope(context).Where(n => n.Status == NoticeStatus.Pending).GroupBy(n => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(n => n.CreatedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("notices.pending", pending?.Count ?? 0, At(pending?.Oldest)));

      var unserved = await context.Notices.InScope(context).Where(n => n.Status == NoticeStatus.Issued && n.ServedOn == null).GroupBy(n => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(n => n.IssueDate) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("notices.unserved", unserved?.Count ?? 0, Day(unserved?.Oldest)));


      var overdue = await context.Demands.InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted)
          && d.Balance > 0 && d.DueDate != null && d.DueDate < today).GroupBy(d => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(d => d.DueDate) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("demands.overdue", overdue?.Count ?? 0, Day(overdue?.Oldest)));

      items.Add(new("demands.defaulted", await context.Demands.InScope(context).CountAsync(d => d.Status == DemandStatus.Defaulted && d.Balance > 0, cancellationToken)));

    }

    // the bulk runs: notices due for their next stage, and years that are over still waiting for the year-end surcharge
    if (Can(PermissionCatalog.Tax.Run))
    {
      var due = await dueNotices.DemandsAsync(today, cancellationToken);
      if (context.IsLimited())
        due = await context.Demands.InScope(context).Where(d => due.Contains(d.Id)).Select(d => d.Id).ToListAsync(cancellationToken);
      items.Add(new("notices.due", due.Count));

      var yearEnd = await context.Demands.InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0 && d.FiscalYear < fy)
        .GroupBy(d => 1).Select(g => new { Count = g.Count(), Oldest = g.Min(d => d.FiscalYear) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("yearend.pending", yearEnd?.Count ?? 0, yearEnd is null ? null : Day(FiscalYear.End(yearEnd.Oldest).AddDays(1))));
    }

    if (Can(PermissionCatalog.Tax.Approve))
    {
      // the in-year surcharge of this year: its period is over and no run applied it yet
      var count = 0;
      DateOnly? from = null;
      var rule = await setup.RuleAsync(PenaltyKind.IntraYear, today, cancellationToken);
      if (rule is not null && !await context.PenaltyRuns.AnyAsync(r => r.FiscalYear == fy, cancellationToken))
      {
        from = rule.EligibleFrom(fy, null, (await setup.SettingsAsync(today, cancellationToken)).DueDate(fy));
        if (from <= today)
        {
          var candidates = context.Demands.InScope(context).Where(d => d.FiscalYear == fy && (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0
            && !d.Penalties.Any(p => p.Kind == PenaltyKind.IntraYear));
          if (rule.TargetSet == TargetSet.DefaultersOnly)
            candidates = candidates.Where(d => d.ArrearsRows.Any(a => a.Outstanding > 0));
          count = await candidates.CountAsync(cancellationToken);
        }
      }
      items.Add(new("surcharge.due", count, count > 0 ? Day(from) : null));
    }

    if (Can(PermissionCatalog.Tax.Issue))
    {
      var expiring = await context.Clearances.InScope(context).Where(c => c.Status == ClearanceStatus.Issued && c.ValidUntil >= today && c.ValidUntil <= today.AddDays(7))
        .GroupBy(c => 1).Select(g => new { Count = g.Count(), Next = g.Min(c => c.ValidUntil) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("clearances.expiring", expiring?.Count ?? 0, null, expiring?.Next));
    }

    if (Can(PermissionCatalog.Tax.Post))
    {
      var fees = await context.Fees.InScope(context).Where(f => f.Status == FeeStatus.Due).GroupBy(f => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(f => f.CreatedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("fees.due", fees?.Count ?? 0, At(fees?.Oldest)));
    }

    // ---- links out and setup (the Section's administrators) ----
    if (Can(PermissionCatalog.TaxSetup.View))
    {
      var failed = await context.PsidSyncs.InScope(context).Where(s => s.Status == PsidSyncStatus.Failed).GroupBy(s => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(s => s.SyncedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("references.failed", failed?.Count ?? 0, At(failed?.Oldest)));
      var waiting = await context.PsidSyncs.InScope(context).Where(s => s.Status == PsidSyncStatus.Pending).GroupBy(s => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(s => s.SyncedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("references.pending", waiting?.Count ?? 0, At(waiting?.Oldest)));
      var queued = await context.SmsMessages.InScope(context).Where(m => m.DeliveryStatus == SmsStatus.Queued).GroupBy(m => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(m => m.QueuedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("sms.queued", queued?.Count ?? 0, At(queued?.Oldest)));
    }
    if (Can(PermissionCatalog.TaxSetup.Approve))
    {
      var drafts = await context.RateSets.Where(s => s.Status == RateSetStatus.Draft).GroupBy(s => 1)
        .Select(g => new { Count = g.Count(), Oldest = g.Min(s => s.CreatedAt) }).FirstOrDefaultAsync(cancellationToken);
      items.Add(new("ratesets.draft", drafts?.Count ?? 0, At(drafts?.Oldest)));
    }
    if (Can(PermissionCatalog.TaxSetup.Edit))
    {
      // what stops the year's first run: no approved rate set in force on 1 July, no town in a circle, no signing officer post
      var start = FiscalYear.Start(fy);
      var blockers = 0;
      if (!await context.RateSets.AnyAsync(s => s.Status != RateSetStatus.Draft && s.EffectiveFrom <= start && (s.EffectiveTo == null || s.EffectiveTo >= start), cancellationToken))
        blockers++;
      if (!await context.CircleTowns.AnyAsync(t => t.EffectiveFrom <= today && (t.EffectiveTo == null || t.EffectiveTo >= today), cancellationToken))
        blockers++;
      var circles = await context.TaxCircles.Where(c => c.IsActive).Select(c => c.Id).ToListAsync(cancellationToken);
      var signed = await context.CircleStaff.Where(s => s.TaxRole == TaxRole.To && s.CircleId != null && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
        .Select(s => s.CircleId!).Distinct().ToListAsync(cancellationToken);
      blockers += circles.Count(c => !signed.Contains(c));
      items.Add(new("setup.blocking", blockers));
    }

    return Result<AttentionResponse>.Success(new AttentionResponse(new AttentionDto(DateTimeOffset.UtcNow, items)));
  }
}
