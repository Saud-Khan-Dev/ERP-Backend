using Microsoft.EntityFrameworkCore;

/// HRM's bulk work for Administration › System. HRM has no scheduled job: every run is started by a person, and each
/// leaves one "Ran" line in the audit trail (its kind is the job's name, its detail what it did). The last line of each
/// job is its last run.
public sealed class HrmJobSource(ApplicationDbContext context) : ISystemJobSource
{
  /// The runs HRM records, in the order the system page lists them.
  public static readonly string[] Jobs =
  [
    HrmRuns.PayrollCalculation, HrmRuns.PayrollFinalization, HrmRuns.SalaryPayments, HrmRuns.Arrears, HrmRuns.AnnualIncrement,
    HrmRuns.PayRevision, HrmRuns.CloseAttendanceDay, HrmRuns.MarkHoliday, HrmRuns.AttendanceImport, HrmRuns.ShiftAssignment, HrmRuns.LeaveEntitlements,
    HrmRuns.LeaveYearEnd, HrmRuns.StartReviews, HrmRuns.GpFundInterest
  ];

  public async Task<IReadOnlyList<SystemJobDto>> JobsAsync(CancellationToken cancellationToken)
  {
    var last = await context.Set<AuditRecord>().AsNoTracking()
      .Where(x => x.Action == AuditActions.Ran && Jobs.Contains(x.RecordKind))
      .OrderByDescending(x => x.At)
      .Select(x => new { x.RecordKind, x.At, x.ActorName, x.Detail })
      .ToListAsync(cancellationToken);
    var byKind = last.GroupBy(x => x.RecordKind).ToDictionary(g => g.Key, g => g.First());

    return Jobs.Select(name => byKind.TryGetValue(name, out var line)
        ? new SystemJobDto(name, line.At, "Succeeded", $"{line.ActorName}: {line.Detail}", null)
        : new SystemJobDto(name, null, null, "Not run yet", null))
      .ToList();
  }
}
