using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// Pay positions: which scale is in force for a grade, and opening / closing an employee's pay record.
public class PayService(IApplicationDbContext context, IOptions<HrmOptions> options)
{
  /// The active scale of a grade on a date, with its stages (tracked).
  public async Task<PayScaleVersion> ScaleInForceAsync(PayScaleGradeId gradeId, DateOnly date, CancellationToken cancellationToken)
  {
    var scale = await context.PayScaleVersions.Include(v => v.Stages)
      .FirstOrDefaultAsync(v => v.GradeId == gradeId && v.Status == RecordStatus.Active
        && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date), cancellationToken);
    if (scale is not null)
      return scale;

    var label = await context.PayScaleGrades.Where(g => g.Id == gradeId).Select(g => g.GradeName ?? "BPS-" + g.BpsNumber).FirstOrDefaultAsync(cancellationToken);
    throw new DomainException($"No pay scale of {label ?? "this grade"} is in force on {date:yyyy-MM-dd}. Notify the scale first.");
  }

  /// The scale a stage belongs to (tracked).
  public async Task<PayScaleVersion> ScaleOfStageAsync(PayScaleStageId stageId, CancellationToken cancellationToken)
  {
    var versionId = await context.PayScaleStages.Where(s => s.Id == stageId).Select(s => s.PayScaleVersionId).FirstOrDefaultAsync(cancellationToken)
      ?? throw new DomainException("The pay scale stage was not found.");
    return await context.PayScaleVersions.Include(v => v.Stages).FirstAsync(v => v.Id == versionId, cancellationToken);
  }

  /// The employee's open pay record (tracked), if any.
  public Task<EmployeePayRecord?> OpenRecordAsync(EmployeeId employeeId, CancellationToken cancellationToken) =>
      context.PayRecords.Where(p => p.EmployeeId == employeeId && p.EffectiveTo == null)
        .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);

  /// The employee's pay record covering a date (tracked), if any.
  public Task<EmployeePayRecord?> RecordOnAsync(EmployeeId employeeId, DateOnly date, CancellationToken cancellationToken) =>
      context.PayRecords.FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo >= date), cancellationToken);

  /// Opens a new pay position from a date; the open one closes the day before. The next increment defaults to the
  /// first annual increment date after the start.
  public async Task<EmployeePayRecord> StartAsync(
      EmployeeId employeeId,
      PayScaleStage stage,
      decimal? basicPay,
      DateOnly effectiveFrom,
      string reason,
      string? orderNumber,
      DateOnly? lastIncrementDate,
      DateOnly? nextIncrementDate,
      CancellationToken cancellationToken)
  {
    var current = await OpenRecordAsync(employeeId, cancellationToken);
    var record = EmployeePayRecord.Open(EmployeePayRecordId.New(), employeeId, current, stage, basicPay, effectiveFrom,
      lastIncrementDate, nextIncrementDate ?? NextIncrementAfter(effectiveFrom), reason, orderNumber);
    context.PayRecords.Add(record);
    return record;
  }

  /// The pay stops after a day (the employee left service).
  public async Task CloseAsync(EmployeeId employeeId, DateOnly lastDay, CancellationToken cancellationToken)
  {
    var current = await OpenRecordAsync(employeeId, cancellationToken);
    if (current is not null && current.EffectiveFrom <= lastDay)
      current.CloseOn(lastDay);
  }

  public DateOnly NextIncrementAfter(DateOnly date) =>
      EmployeePayRecord.NextIncrementAfter(date, options.Value.AnnualIncrementMonth, options.Value.AnnualIncrementDay);
}
