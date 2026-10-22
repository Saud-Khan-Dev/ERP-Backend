using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GetPayRecordsQueryResult(IReadOnlyList<PayRecordDto> PayRecords);

public sealed record GetPayRecordsQuery(Guid EmployeeId) : IQuery<Result<GetPayRecordsQueryResult>>;

/// A new pay position from a date (e.g. a correction, advance increments, personal pay): a stage of the scale in force
/// for the given grade, and optionally a basic pay different from the stage's. The open record closes the day before.
public sealed record RecordPayChangeCommand(
  Guid EmployeeId,
  Guid PayScaleStageId,
  decimal? BasicPay,
  DateOnly EffectiveFrom,
  string Reason,
  string? OrderNumber,
  DateOnly? NextIncrementDate) : ICommand<Result<RecordPayChangeResult>>;

public sealed record RecordPayChangeResult(Guid Id, IReadOnlyList<PayrollImpactDto> PayrollImpact);

public sealed record SetNextIncrementDateCommand(Guid PayRecordId, DateOnly? NextIncrementDate, string? Reason = null) : ICommand<Result<UpdatedResult>>;

public sealed record PayRunSkip(Guid EmployeeId, string EmployeeNumber, string Reason);

public sealed record PayRunResult(int Considered, int Moved, IReadOnlyList<PayRunSkip> Skipped, bool DryRun);

/// The annual increment (1 December in KP): every employee in service whose next increment falls on or before the date
/// moves one stage up their scale. Employees at the top of the scale are reported, not moved; leave without pay puts the
/// increment back by its days (reported as deferred). DryRun saves nothing.
public sealed record RunAnnualIncrementCommand(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? EmployeeIds) : ICommand<Result<PayRunResult>>;

/// A pay revision: everyone on an older scale of a grade whose new scale starts on the date moves to the same stage
/// number of the new scale (the top stage when the new scale is shorter). DryRun saves nothing.
public sealed record ApplyPayRevisionCommand(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? GradeIds) : ICommand<Result<PayRunResult>>;

public class RecordPayChangeCommandValidator : AbstractValidator<RecordPayChangeCommand>
{
  public RecordPayChangeCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.PayScaleStageId).NotEmpty();
    RuleFor(x => x.BasicPay).GreaterThanOrEqualTo(0).When(x => x.BasicPay.HasValue);
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
    RuleFor(x => x.OrderNumber).MaximumLength(100);
  }
}

public class PayRecordHandlers(IApplicationDbContext context, PayService pay, HrLookup lookup, ServiceRecordReader reader, ChangeLog log,
  PayrollImpactFinder payroll, IClock clock, Sensitive sensitive, DutyGuard duty, IAuditTrail audit, ICurrentUser currentUser, OfficeScope scope) :
  IQueryHandler<GetPayRecordsQuery, Result<GetPayRecordsQueryResult>>,
  ICommandHandler<RecordPayChangeCommand, Result<RecordPayChangeResult>>,
  ICommandHandler<SetNextIncrementDateCommand, Result<UpdatedResult>>,
  ICommandHandler<RunAnnualIncrementCommand, Result<PayRunResult>>,
  ICommandHandler<ApplyPayRevisionCommand, Result<PayRunResult>>
{
  public async Task<Result<GetPayRecordsQueryResult>> Handle(GetPayRecordsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var records = await context.PayRecords.AsNoTracking().Where(p => p.EmployeeId == employeeId).OrderByDescending(p => p.EffectiveFrom).ToListAsync(cancellationToken);
    var stageIds = records.Select(r => r.PayScaleStageId).Distinct().ToList();
    var stages = await context.PayScaleStages.AsNoTracking().Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
    var versionIds = stages.Values.Select(s => s.PayScaleVersionId).Distinct().ToList();
    var versions = await context.PayScaleVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);

    var today = clock.Today;
    var shows = sensitive.CanView;
    var withheld = sensitive.Withheld(false, Sensitive.BasicPay);
    var data = records.Select(r =>
    {
      var stage = stages[r.PayScaleStageId];
      var version = versions[stage.PayScaleVersionId];
      return new PayRecordDto(r.Id.Value, stage.Id.Value, version.Id.Value, version.VersionLabel, grades[version.GradeId.Value].BpsNumber,
        stage.StageNumber, shows ? r.BasicPay : null, r.LastIncrementDate, r.NextIncrementDate, r.Reason, r.OrderNumber, r.EffectiveFrom, r.EffectiveTo,
        r.CreatedAt, r.IsIncrementOverdue(today), r.IncrementDeferredDays, withheld);
    }).ToList();

    return Result<GetPayRecordsQueryResult>.Success(new(data));
  }

  public async Task<Result<RecordPayChangeResult>> Handle(RecordPayChangeCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "change the pay", employee, cancellationToken);
    employee.EnsureInService();
    var date = command.EffectiveFrom;

    var stageId = PayScaleStageId.Of(command.PayScaleStageId);
    var scale = await pay.ScaleOfStageAsync(stageId, cancellationToken);
    if (scale.Status != RecordStatus.Active || !scale.Range.Contains(date))
      return Result<RecordPayChangeResult>.Failure($"That stage belongs to a scale not in force on {date:dd MMM yyyy}.");

    // the pay follows the grade of the regular post held that day
    var held = await reader.RegularPostOnAsync(employee.Id, date, cancellationToken);
    if (held is null)
      return Result<RecordPayChangeResult>.Failure($"{employee.DisplayName} holds no regular post on {date:dd MMM yyyy}; the pay follows the post's grade. Place the employee on a post first.");
    if (held.Version.GradeId != scale.GradeId)
    {
      var grades = await lookup.GradesAsync(cancellationToken);
      var postBps = grades[held.Version.GradeId.Value].BpsNumber;
      var stageBps = grades[scale.GradeId.Value].BpsNumber;
      return Result<RecordPayChangeResult>.Failure($"Post {held.Post.PostCode} is BPS-{postBps} on {date:dd MMM yyyy}, but that stage is of the BPS-{stageBps} scale. "
        + $"Choose a stage of the BPS-{postBps} scale; a move to another grade is a promotion or a demotion.");
    }

    var before = await pay.RecordOnAsync(employee.Id, date.AddDays(-1), cancellationToken);
    var stage = scale.Stage(stageId)!;
    var record = await pay.StartAsync(employee.Id, stage, command.BasicPay, date, command.Reason,
      command.OrderNumber, null, command.NextIncrementDate, cancellationToken);
    log.Add(employee.Id, EmployeeFields.Pay, before is null ? null : $"Rs {before.BasicPay:#,##0.##}", $"Rs {record.BasicPay:#,##0.##} (stage {stage.StageNumber}) from {date:yyyy-MM-dd}",
      command.Reason);

    var impact = before is null || before.BasicPay == record.BasicPay
      ? []
      : await payroll.RunsAsync(employee.Id, date, null, record.BasicPay > before.BasicPay ? PayrollImpactFinder.Arrears : PayrollImpactFinder.Recovery, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<RecordPayChangeResult>.Success(new(record.Id.Value, impact));
  }

  public async Task<Result<UpdatedResult>> Handle(SetNextIncrementDateCommand command, CancellationToken cancellationToken)
  {
    var recordId = EmployeePayRecordId.Of(command.PayRecordId);
    var record = await context.PayRecords.FirstOrDefaultAsync(p => p.Id == recordId, cancellationToken)
      ?? throw new PayRecordNotFoundException($"Pay record {command.PayRecordId} was not found.");
    if (!record.IsOpen)
      return Result<UpdatedResult>.Failure("Only the current pay record has a next increment.");
    await duty.EnsureNotOwnRecordAsync(record.EmployeeId, "set the next increment date", record, cancellationToken);

    var old = record.NextIncrementDate;
    record.SetNextIncrementDate(command.NextIncrementDate);
    log.Add(record.EmployeeId, EmployeeFields.NextIncrement, EmployeeChangeLogEntry.Day(old), EmployeeChangeLogEntry.Day(record.NextIncrementDate), command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<PayRunResult>> Handle(RunAnnualIncrementCommand command, CancellationToken cancellationToken)
  {
    var date = command.EffectiveDate;
    var open = context.PayRecords.Where(p => p.EffectiveTo == null && p.EffectiveFrom < date && p.NextIncrementDate != null && p.NextIncrementDate <= date);
    if (command.EmployeeIds is { Count: > 0 })
    {
      var only = command.EmployeeIds.Select(EmployeeId.Of).ToList();
      open = open.Where(p => only.Contains(p.EmployeeId));
    }

    var records = await open.ToListAsync(cancellationToken);
    var employees = await EmployeesAsync(records.Select(r => r.EmployeeId), cancellationToken);
    var scales = await ScalesOfAsync(records.Select(r => r.PayScaleStageId), cancellationToken);
    var unpaid = await UnpaidLeaveAsync(records.Select(r => r.EmployeeId), cancellationToken);
    var skipped = new List<PayRunSkip>();
    var moved = 0;
    var ownRule = await duty.IsOnAsync(DutyRules.OwnRecord, cancellationToken);

    foreach (var record in records)
    {
      var employee = employees[record.EmployeeId];
      if (ownRule && currentUser.EmployeeId == employee.Id.Value)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, "Your own record: nobody decides on their own pay. Someone else runs it for you."));
        continue;
      }
      if (employee.HasLeftService || employee.EmploymentStatus == EmploymentStatus.Suspended || employee.ProfileStatus != RecordStatus.Active)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, $"Not in service ({EnumText.Words(employee.EmploymentStatus)})."));
        continue;
      }

      // leave without pay does not count towards the increment: it falls that many days later
      var leave = unpaid.GetValueOrDefault(record.EmployeeId) ?? [];
      if (record.DeferIncrement((from, to) => leave.Sum(r => r.Clip(from, to)?.Days ?? 0)) && record.NextIncrementDate > date)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber,
          $"Deferred to {record.NextIncrementDate:dd MMM yyyy} by {record.IncrementDeferredDays} day(s) of leave without pay."));
        continue;
      }

      var scale = scales[record.PayScaleStageId];
      var stage = scale.Stage(record.PayScaleStageId)!;
      var next = scale.NextStage(stage.StageNumber);
      if (next is null)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, $"At the top of the scale (stage {stage.StageNumber})."));
        continue;
      }

      var newRecord = EmployeePayRecord.Open(EmployeePayRecordId.New(), employee.Id, record, next, null, date, date,
        pay.NextIncrementAfter(date), PayChangeReasons.AnnualIncrement, null);
      context.PayRecords.Add(newRecord);
      moved++;
    }

    if (!command.DryRun)
      await SaveRunAsync(HrmRuns.AnnualIncrement, $"Effective {date:yyyy-MM-dd}", records.Count, moved, skipped.Count, cancellationToken);

    return Result<PayRunResult>.Success(new(records.Count, moved, skipped, command.DryRun));
  }

  public async Task<Result<PayRunResult>> Handle(ApplyPayRevisionCommand command, CancellationToken cancellationToken)
  {
    var date = command.EffectiveDate;
    var newScales = context.PayScaleVersions.Include(v => v.Stages).Where(v => v.Status == RecordStatus.Active && v.EffectiveFrom == date);
    if (command.GradeIds is { Count: > 0 })
    {
      var only = command.GradeIds.Select(PayScaleGradeId.Of).ToList();
      newScales = newScales.Where(v => only.Contains(v.GradeId));
    }

    var revised = (await newScales.ToListAsync(cancellationToken)).ToDictionary(v => v.GradeId);
    if (revised.Count == 0)
      return Result<PayRunResult>.Failure($"No pay scale starts on {date:yyyy-MM-dd}. Notify the revised scales first.");

    var revisedGrades = revised.Keys.ToList();
    var oldStageIds = await context.PayScaleStages
      .Where(s => context.PayScaleVersions.Any(v => v.Id == s.PayScaleVersionId && revisedGrades.Contains(v.GradeId) && v.EffectiveFrom < date))
      .Select(s => s.Id).ToListAsync(cancellationToken);

    var records = await context.PayRecords
      .Where(p => p.EffectiveTo == null && p.EffectiveFrom < date && oldStageIds.Contains(p.PayScaleStageId))
      .ToListAsync(cancellationToken);
    var employees = await EmployeesAsync(records.Select(r => r.EmployeeId), cancellationToken);
    var scales = await ScalesOfAsync(records.Select(r => r.PayScaleStageId), cancellationToken);
    var skipped = new List<PayRunSkip>();
    var moved = 0;

    var ownRule = await duty.IsOnAsync(DutyRules.OwnRecord, cancellationToken);
    foreach (var record in records)
    {
      var employee = employees[record.EmployeeId];
      if (ownRule && currentUser.EmployeeId == employee.Id.Value)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, "Your own record: nobody decides on their own pay. Someone else runs it for you."));
        continue;
      }
      if (employee.HasLeftService)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, $"Not in service ({EnumText.Words(employee.EmploymentStatus)})."));
        continue;
      }

      var oldScale = scales[record.PayScaleStageId];
      var stageNumber = oldScale.Stage(record.PayScaleStageId)!.StageNumber;
      var newScale = revised[oldScale.GradeId];
      var stage = newScale.Stage(stageNumber) ?? newScale.Stages.OrderByDescending(s => s.StageNumber).First();

      context.PayRecords.Add(EmployeePayRecord.Open(EmployeePayRecordId.New(), employee.Id, record, stage, null, date,
        record.LastIncrementDate, record.NextIncrementDate, PayChangeReasons.PayRevision, newScale.NotificationRef));
      moved++;
    }

    if (!command.DryRun)
      await SaveRunAsync(HrmRuns.PayRevision, $"Revised scales from {date:yyyy-MM-dd} ({revised.Count} grade{(revised.Count == 1 ? "" : "s")})",
        records.Count, moved, skipped.Count, cancellationToken);

    return Result<PayRunResult>.Success(new(records.Count, moved, skipped, command.DryRun));
  }

  /// A run's pay records are saved without a line each; the run writes one line saying who ran it and what it did.
  private async Task SaveRunAsync(string run, string what, int considered, int moved, int skipped, CancellationToken cancellationToken)
  {
    using (audit.Quietly())
    {
      var offices = scope.IsLimited ? $" · offices: {await scope.OfficeNamesAsync(cancellationToken)}" : "";
      audit.Record(AuditActions.Ran, run, detail: $"{what} · {considered} considered · {moved} moved · {skipped} skipped{offices}");
      await context.SaveChangesAsync(cancellationToken);
    }
  }

  /// Each employee's approved leave without pay (types deducted from pay), as date ranges.
  private async Task<Dictionary<EmployeeId, List<DateRange>>> UnpaidLeaveAsync(IEnumerable<EmployeeId> ids, CancellationToken cancellationToken)
  {
    var list = ids.Distinct().ToList();
    var rows = await (
        from a in context.LeaveApplications.AsNoTracking()
        where list.Contains(a.EmployeeId) && a.Status == LeaveStatus.Approved
        join t in context.LeaveTypes on a.LeaveTypeId equals t.Id
        where t.AffectsPayroll
        select new { a.EmployeeId, a.StartDate, a.EndDate })
      .ToListAsync(cancellationToken);
    return rows.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Select(r => new DateRange(r.StartDate, r.EndDate)).ToList());
  }

  private async Task<Dictionary<EmployeeId, Employee>> EmployeesAsync(IEnumerable<EmployeeId> ids, CancellationToken cancellationToken)
  {
    var list = ids.Distinct().ToList();
    return await context.Employees.AsNoTracking().Where(e => list.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
  }

  /// Each stage's scale (with all its stages).
  private async Task<Dictionary<PayScaleStageId, PayScaleVersion>> ScalesOfAsync(IEnumerable<PayScaleStageId> stageIds, CancellationToken cancellationToken)
  {
    var list = stageIds.Distinct().ToList();
    var versionIds = await context.PayScaleStages.AsNoTracking().Where(s => list.Contains(s.Id)).Select(s => s.PayScaleVersionId).Distinct().ToListAsync(cancellationToken);
    var versions = await context.PayScaleVersions.AsNoTracking().Include(v => v.Stages).Where(v => versionIds.Contains(v.Id)).ToListAsync(cancellationToken);
    return versions.SelectMany(v => v.Stages.Select(s => (s.Id, Version: v))).Where(x => list.Contains(x.Id)).ToDictionary(x => x.Id, x => x.Version);
  }
}
