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
  DateOnly? NextIncrementDate) : ICommand<Result<CreatedResult>>;

public sealed record SetNextIncrementDateCommand(Guid PayRecordId, DateOnly? NextIncrementDate) : ICommand<Result<UpdatedResult>>;

public sealed record PayRunSkip(Guid EmployeeId, string EmployeeNumber, string Reason);

public sealed record PayRunResult(int Considered, int Moved, IReadOnlyList<PayRunSkip> Skipped, bool DryRun);

/// The annual increment (1 December in KP): every employee in service whose next increment falls on or before the date
/// moves one stage up their scale. Employees at the top of the scale are reported, not moved. DryRun saves nothing.
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

public class PayRecordHandlers(IApplicationDbContext context, PayService pay, HrLookup lookup) :
  IQueryHandler<GetPayRecordsQuery, Result<GetPayRecordsQueryResult>>,
  ICommandHandler<RecordPayChangeCommand, Result<CreatedResult>>,
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

    var data = records.Select(r =>
    {
      var stage = stages[r.PayScaleStageId];
      var version = versions[stage.PayScaleVersionId];
      return new PayRecordDto(r.Id.Value, stage.Id.Value, version.Id.Value, version.VersionLabel, grades[version.GradeId.Value].BpsNumber,
        stage.StageNumber, r.BasicPay, r.LastIncrementDate, r.NextIncrementDate, r.Reason, r.OrderNumber, r.EffectiveFrom, r.EffectiveTo, r.CreatedAt);
    }).ToList();

    return Result<GetPayRecordsQueryResult>.Success(new(data));
  }

  public async Task<Result<CreatedResult>> Handle(RecordPayChangeCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureInService();

    var stageId = PayScaleStageId.Of(command.PayScaleStageId);
    var scale = await pay.ScaleOfStageAsync(stageId, cancellationToken);
    if (scale.Status != RecordStatus.Active || !scale.Range.Contains(command.EffectiveFrom))
      return Result<CreatedResult>.Failure($"That stage belongs to a scale not in force on {command.EffectiveFrom:yyyy-MM-dd}.");

    var record = await pay.StartAsync(employee.Id, scale.Stage(stageId)!, command.BasicPay, command.EffectiveFrom, command.Reason,
      command.OrderNumber, null, command.NextIncrementDate, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(record.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(SetNextIncrementDateCommand command, CancellationToken cancellationToken)
  {
    var recordId = EmployeePayRecordId.Of(command.PayRecordId);
    var record = await context.PayRecords.FirstOrDefaultAsync(p => p.Id == recordId, cancellationToken)
      ?? throw new PayRecordNotFoundException($"Pay record {command.PayRecordId} was not found.");
    if (!record.IsOpen)
      return Result<UpdatedResult>.Failure("Only the current pay record has a next increment.");

    record.SetNextIncrementDate(command.NextIncrementDate);
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
    var skipped = new List<PayRunSkip>();
    var moved = 0;

    foreach (var record in records)
    {
      var employee = employees[record.EmployeeId];
      if (employee.HasLeftService || employee.EmploymentStatus == EmploymentStatus.Suspended || employee.ProfileStatus != RecordStatus.Active)
      {
        skipped.Add(new(employee.Id.Value, employee.EmployeeNumber, $"Not in service ({EnumText.Words(employee.EmploymentStatus)})."));
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
      await context.SaveChangesAsync(cancellationToken);

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

    foreach (var record in records)
    {
      var employee = employees[record.EmployeeId];
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
      await context.SaveChangesAsync(cancellationToken);

    return Result<PayRunResult>.Success(new(records.Count, moved, skipped, command.DryRun));
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
