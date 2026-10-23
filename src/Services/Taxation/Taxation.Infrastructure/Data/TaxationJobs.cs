using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

/// Taxation's scheduled and bulk jobs for Administration › System: the daily job (from job_run), the last demand run and
/// the last in-year surcharge run.
public sealed class TaxationJobs(ApplicationDbContext context, IConfiguration configuration) : ISystemJobSource
{
  private static readonly TimeSpan Pkt = TimeSpan.FromHours(5);

  public async Task<IReadOnlyList<SystemJobDto>> JobsAsync(CancellationToken cancellationToken)
  {
    var jobs = new List<SystemJobDto>();

    var enabled = configuration.GetValue("Taxation:DailyJob:Enabled", true);
    var hour = Math.Clamp(configuration.GetValue("Taxation:DailyJob:HourPkt", 6), 0, 23);
    var last = await context.JobRuns.AsNoTracking().Where(r => r.Job == JobRun.Daily).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
    var now = DateTimeOffset.UtcNow.ToOffset(Pkt);
    var todayAt = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, Pkt);
    var doneToday = last is { Status: JobRunStatus.Done } && last.RunDate == DateOnly.FromDateTime(now.DateTime);
    DateTimeOffset? next = !enabled ? null : doneToday || now > todayAt.AddHours(20) ? todayAt.AddDays(1) : todayAt > now ? todayAt : now.AddMinutes(15);
    jobs.Add(new SystemJobDto(
      "Taxation daily job (year-end surcharge, notices due)",
      last is null ? null : At(last.StartedAt),
      last?.Status switch { JobRunStatus.Done => "Succeeded", JobRunStatus.Failed => "Failed", JobRunStatus.Running => "Running", _ => null },
      !enabled ? "Switched off (Taxation:DailyJob:Enabled)."
        : last is null ? $"Runs every day from {hour:00}:00 Pakistan time; it has not run yet."
        : $"{(last.StartedBy is null ? "By the schedule" : "Run by a person")}: {last.Error ?? last.Summary ?? "running"}",
      next));

    var run = await context.DemandRuns.AsNoTracking().OrderByDescending(r => r.ComputedAt).FirstOrDefaultAsync(cancellationToken);
    if (run is not null)
    {
      var circle = run.CircleId is { } c ? await context.TaxCircles.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken) : null;
      jobs.Add(new SystemJobDto("Taxation demand run", run.ComputedAt is { } at ? At(at) : null, "Succeeded",
        $"FY {FiscalYear.Label(run.FiscalYear)} · {circle ?? "all circles"} · {run.PropertyCount ?? 0} demands · {EnumText.Words(run.Status).ToLowerInvariant()}"
        + (run.ApprovedAt is { } approved ? $" on {At(approved).ToOffset(Pkt):dd MMM yyyy}" : ""), null));
    }

    var surcharge = await context.PenaltyRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    if (surcharge is not null)
      jobs.Add(new SystemJobDto("Taxation in-year surcharge run", surcharge.CreatedAt is { } created ? At(created) : null, "Succeeded",
        $"FY {FiscalYear.Label(surcharge.FiscalYear)} · {surcharge.PropertyCount ?? 0} demands surcharged, Rs {surcharge.TotalAmount ?? 0:N0}", null));

    return jobs;
  }

  private static DateTimeOffset At(DateTime utc) => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
