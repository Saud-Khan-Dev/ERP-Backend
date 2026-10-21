using Microsoft.EntityFrameworkCore;

/// The unpaid demands of the year whose next notice is due today, as the daily job would prepare it: none pending, the
/// file not on hold, the stage's day reached (weekends skipped; HRM holidays are not read here, so a holiday can make one
/// a day early). For the bell and for the demands register's "next notice due" view.
public sealed class DueNotices(IApplicationDbContext context, TaxSetup setup)
{
  public async Task<List<DemandId>> DemandsAsync(DateOnly today, CancellationToken cancellationToken)
  {
    var fy = FiscalYear.Of(today);
    var demands = await context.Demands.AsNoTracking()
      .Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0 && d.IssuedOn != null && d.FiscalYear >= fy)
      .Select(d => new { d.Id, d.TaxFileId, d.IssuedOn }).ToListAsync(cancellationToken);
    if (demands.Count == 0)
      return [];
    var ids = demands.Select(d => d.Id).ToList();
    var notices = await context.Notices.AsNoTracking().Where(n => ids.Contains(n.DemandId)).Select(n => new { n.DemandId, n.Stage, n.Status, n.IssueDate })
      .ToListAsync(cancellationToken);
    var fileIds = demands.Select(d => d.TaxFileId).Distinct().ToList();
    var held = (await context.Holds.AsNoTracking().Where(h => fileIds.Contains(h.TaxFileId) && h.SuspendsDemand && h.StartedOn <= today && (h.EndedOn == null || h.EndedOn >= today))
      .Select(h => h.TaxFileId).ToListAsync(cancellationToken)).ToHashSet();
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var due = new List<DemandId>();
    foreach (var d in demands)
    {
      if (held.Contains(d.TaxFileId))
        continue;
      var mine = notices.Where(n => n.DemandId == d.Id).ToList();
      if (mine.Any(n => n.Status == NoticeStatus.Pending))
        continue;
      var ladder = mine.Where(n => NoticeLadder.Stages.Contains(n.Stage)).ToList();
      NoticeStage? reached = ladder.Count == 0 ? null : ladder.MaxBy(n => Array.IndexOf(NoticeLadder.Stages, n.Stage))!.Stage;
      var first = mine.Where(n => n.Stage == NoticeStage.First && n.Status == NoticeStatus.Issued).Select(n => (DateOnly?)n.IssueDate).Min() ?? d.IssuedOn!.Value;
      if (NoticeLadder.IsDue(reached, first, today, settings))
        due.Add(d.Id);
    }
    return due;
  }
}
