using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// The daily job: the year-end surcharges of years that are over (the demands become defaulted), then the notices due for
/// their next stage, prepared for confirmation. Each step is idempotent. The run is recorded in taxation.job_run (its own
/// save, so a failure is recorded too); run by the schedule (DailyJobService) or at once by an officer.
///
/// The audit trail gets one "Ran" line per run (what it did, or why it failed) instead of a line per surcharge and notice;
/// a scheduled run is written as "System · Daily job", a run started by a person under their name.
public sealed class DailyJob(IServiceScopeFactory scopes, ILogger<DailyJob> logger)
{
  public const string AuditKind = "Daily job";

  public async Task<JobRunDto> RunAsync(Guid? startedBy, CancellationToken cancellationToken)
  {
    using var system = startedBy is null ? AuditScope.ActingAs("Daily job") : null;
    using var quiet = AuditScope.Quietly();
    JobRunId id;
    using (var scope = scopes.CreateScope())
    {
      var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
      var clock = scope.ServiceProvider.GetRequiredService<IClock>();
      var run = JobRun.Start(JobRunId.New(), JobRun.Daily, clock.Today, startedBy, clock.UtcNow);
      context.JobRuns.Add(run);
      await context.SaveChangesAsync(cancellationToken);
      id = run.Id;
    }

    try
    {
      using var scope = scopes.CreateScope();
      var sp = scope.ServiceProvider;
      var context = sp.GetRequiredService<IApplicationDbContext>();
      var clock = sp.GetRequiredService<IClock>();
      var calendars = sp.GetRequiredService<CalendarProvider>();
      var yearEnd = await sp.GetRequiredService<PenaltyService>().ApplyYearEndAsync(cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      var prepared = await sp.GetRequiredService<NoticeEscalation>().PrepareDueAsync(cancellationToken);
      var run = await context.JobRuns.FirstAsync(r => r.Id == id, cancellationToken);
      var summary = $"{yearEnd} year-end surcharge(s) applied; {prepared.Count} notice(s) prepared for confirmation"
        + (calendars.FellBack ? "; HRM holidays could not be read, so notice days skipped weekends only" : "") + ".";
      run.Finish(yearEnd, prepared.Count, summary, clock.UtcNow);
      sp.GetRequiredService<IAuditTrail>().Record(AuditActions.Ran, AuditKind, run.Id.Value, run.RunDate.ToString("yyyy-MM-dd"), null, null, null, summary);
      await context.SaveChangesAsync(cancellationToken);
      return run.ToDto();
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "The taxation daily job failed.");
      using var scope = scopes.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
      var clock = scope.ServiceProvider.GetRequiredService<IClock>();
      var run = await context.JobRuns.FirstAsync(r => r.Id == id, cancellationToken);
      run.Fail(ex is DomainException d ? DomainMessages.Text(d) : ex.Message, clock.UtcNow);
      scope.ServiceProvider.GetRequiredService<IAuditTrail>().Record(AuditActions.Ran, AuditKind, run.Id.Value, run.RunDate.ToString("yyyy-MM-dd"), null, null, null,
        "Failed: " + run.Error);
      await context.SaveChangesAsync(cancellationToken);
      return run.ToDto();
    }
  }

  /// Whether the schedule should run now: the day's hour has come and no run of today finished (a failed run is tried
  /// again an hour later, at most three times a day).
  public async Task<bool> DueAsync(int hourPkt, CancellationToken cancellationToken)
  {
    using var scope = scopes.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();
    if (clock.UtcNow.AddHours(5).Hour < hourPkt)
      return false;
    var today = clock.Today;
    var runs = await context.JobRuns.AsNoTracking().Where(r => r.Job == JobRun.Daily && r.RunDate == today).ToListAsync(cancellationToken);
    if (runs.Any(r => r.Status == JobRunStatus.Done || (r.Status == JobRunStatus.Running && r.StartedAt > clock.UtcNow.AddHours(-2))))
      return false;
    var failed = runs.Where(r => r.Status == JobRunStatus.Failed).ToList();
    return failed.Count < 3 && failed.All(r => r.FinishedAt < clock.UtcNow.AddHours(-1));
  }
}
