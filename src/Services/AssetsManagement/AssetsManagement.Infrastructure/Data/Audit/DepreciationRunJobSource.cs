using Microsoft.EntityFrameworkCore;

/// Administration › System's view of the depreciation run: the last run over every asset, read from its "Ran" line in
/// the audit trail (a run for one asset, from its record, is not the month-end job). Nobody schedules it - a finance
/// officer runs it at month end - so there is no next run.
public sealed class DepreciationRunJobSource(ApplicationDbContext context) : ISystemJobSource
{
  public const string JobName = "Depreciation run (every asset)";

  public async Task<IReadOnlyList<SystemJobDto>> JobsAsync(CancellationToken cancellationToken)
  {
    var last = await context.Set<AuditRecord>().AsNoTracking()
      .Where(x => x.RecordKind == AssetAuditKinds.DepreciationRun && x.Action == AuditActions.Ran && x.ParentId == null)
      .OrderByDescending(x => x.At)
      .FirstOrDefaultAsync(cancellationToken);

    return
    [
      last is null
        ? new SystemJobDto(JobName, null, null, "Not run yet. A finance officer runs it at month end (Assets › Lifecycle › Depreciation run).", null)
        : new SystemJobDto(JobName, last.At, "Succeeded", AssetAudit.Join($"By {last.ActorName}", last.RecordLabel, last.Detail), null)
    ];
  }
}
