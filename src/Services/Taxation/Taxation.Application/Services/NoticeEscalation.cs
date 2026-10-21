using Microsoft.EntityFrameworkCore;

/// The notice ladder of the year: first (with the demand), then second, final and show cause, each once its day has come
/// (days counted from the first notice, in working days by the HRM calendar). Used by "Prepare due notices" and by the
/// daily job; safe to run any number of times a day.
public sealed class NoticeEscalation(IApplicationDbContext context, TaxSetup setup, NoticeComposer composer, CalendarProvider calendars, IClock clock)
{
  public static readonly NoticeStage[] Ladder = NoticeLadder.Stages;

  /// Prepares, dated today and pending confirmation, the next notice of every unpaid demand of a year not over yet:
  /// - only a stage above the highest the demand already reached (one made by hand counts, and so does one cancelled
  ///   on purpose - it is not prepared again);
  /// - a demand with no notice yet (its file was on hold when the run was approved) gets its first;
  /// - none while the demand has a notice pending, or its file is on hold.
  public async Task<List<Notice>> PrepareDueAsync(CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var demands = await context.DemandsWithItems()
      .Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0 && d.IssuedOn != null)
      .ToListAsync(cancellationToken);
    demands = demands.Where(d => today <= FiscalYear.End(d.FiscalYear)).ToList();
    var prepared = new List<Notice>();
    if (demands.Count == 0)
      return prepared;

    var ids = demands.Select(d => d.Id).ToList();
    var notices = await context.Notices.AsNoTracking().Where(n => ids.Contains(n.DemandId))
      .Select(n => new { n.DemandId, n.Stage, n.Status, n.IssueDate }).ToListAsync(cancellationToken);
    var fileIds = demands.Select(d => d.TaxFileId).Distinct().ToList();
    var files = (await context.TaxFilesWithDetails().Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken)).ToDictionary(f => f.Id);
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var from = demands.Min(d => d.IssuedOn!.Value);
    var calendar = await calendars.CalendarAsync(settings, from, today.AddDays(1), cancellationToken);
    var steps = new[] { (NoticeStage.Second, settings.NoticeSecondDays), (NoticeStage.Final, settings.NoticeFinalDays), (NoticeStage.ShowCause, settings.NoticeShowCauseDays) };

    foreach (var demand in demands)
    {
      var file = files[demand.TaxFileId];
      var mine = notices.Where(n => n.DemandId == demand.Id).ToList();
      if (mine.Any(n => n.Status == NoticeStatus.Pending) || file.HoldOn(today) is not null)
        continue;

      var reached = mine.Where(n => Ladder.Contains(n.Stage)).Select(n => Array.IndexOf(Ladder, n.Stage)).DefaultIfEmpty(-1).Max();
      if (reached < 0)
      {
        prepared.Add(await composer.PrepareAsync(demand, file, NoticeStage.First, NoticeOrigin.Cycle, today, null, cancellationToken));
        continue;
      }

      // the days run from the first notice (issued with the demand, or later when the file was on hold then)
      var first = mine.Where(n => n.Stage == NoticeStage.First && n.Status == NoticeStatus.Issued).Select(n => (DateOnly?)n.IssueDate).Min() ?? demand.IssuedOn!.Value;
      foreach (var (stage, days) in steps)
      {
        if (Array.IndexOf(Ladder, stage) <= reached)
          continue;
        var due = calendar.AddDays(first, days);
        if (due <= today)
          prepared.Add(await composer.PrepareAsync(demand, file, stage, NoticeOrigin.Cycle, today, null, cancellationToken));
        break;
      }
    }
    return prepared;
  }
}
