/// taxation.job_run: one run of the daily job - the year-end surcharges of years that are over, then the notices due for
/// their next stage, prepared for confirmation. Run once a day by the service (or at once by an approver); a day already
/// run is not run again by the schedule, and each step is idempotent anyway.
public class JobRun : Aggregate<JobRunId>
{
  public const string Daily = "DAILY";

  public string Job { get; private set; } = default!;
  public DateOnly RunDate { get; private set; }
  public DateTime StartedAt { get; private set; }
  public DateTime? FinishedAt { get; private set; }
  public JobRunStatus Status { get; private set; }
  /// Who started it at once (null: the schedule).
  public Guid? StartedBy { get; private set; }
  public int YearEndSurcharged { get; private set; }
  public int NoticesPrepared { get; private set; }
  public string? Summary { get; private set; }
  public string? Error { get; private set; }

  private JobRun() { }

  public static JobRun Start(JobRunId id, string job, DateOnly runDate, Guid? startedBy, DateTime now) => new()
  {
    Id = id,
    Job = Guard.RequiredText(job, 20, "Job"),
    RunDate = runDate,
    StartedAt = now,
    StartedBy = startedBy,
    Status = JobRunStatus.Running
  };

  public void Finish(int yearEnd, int prepared, string? summary, DateTime now)
  {
    YearEndSurcharged = yearEnd;
    NoticesPrepared = prepared;
    Summary = Clip(summary, 500);
    FinishedAt = now;
    Status = JobRunStatus.Done;
  }

  public void Fail(string error, DateTime now)
  {
    Error = Clip(error, 1000);
    FinishedAt = now;
    Status = JobRunStatus.Failed;
  }

  private static string? Clip(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..(max - 3)] + "...";
}
