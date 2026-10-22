using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GetPayrollPeriodsQueryResult(IReadOnlyList<PayrollPeriodDto> Periods);
public sealed record GetPayrollPeriodsQuery(int? Year) : IQuery<Result<GetPayrollPeriodsQueryResult>>;

/// Opens a payroll month (the calendar month unless other dates are given).
public sealed record CreatePayrollPeriodCommand(int Year, int Month, DateOnly? StartDate, DateOnly? EndDate) : ICommand<Result<CreatedResult>>;

public enum PayrollPeriodAction
{
  Close,
  Reopen,
  Lock
}

public sealed record ChangePayrollPeriodStatusCommand(Guid Id, PayrollPeriodAction Action) : ICommand<Result<UpdatedResult>>;

/// New dates for an open month that has no run yet.
public sealed record UpdatePayrollPeriodDatesCommand(Guid Id, DateOnly StartDate, DateOnly EndDate) : ICommand<Result<UpdatedResult>>;

/// Deletes an open month that has no run (one opened by mistake).
public sealed record DeletePayrollPeriodCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
  public CreatePayrollPeriodCommandValidator()
  {
    RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    RuleFor(x => x.Month).InclusiveBetween(1, 12);
  }
}

public class PayrollPeriodHandlers(IApplicationDbContext context, OfficeScope scope, IAuditTrail audit) :
  IQueryHandler<GetPayrollPeriodsQuery, Result<GetPayrollPeriodsQueryResult>>,
  ICommandHandler<CreatePayrollPeriodCommand, Result<CreatedResult>>,
  ICommandHandler<ChangePayrollPeriodStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<UpdatePayrollPeriodDatesCommand, Result<UpdatedResult>>,
  ICommandHandler<DeletePayrollPeriodCommand, Result<UpdatedResult>>
{
  /// Runs not yet finalized (a month cannot close while one is open).
  private static readonly PayrollRunStatus[] Unfinished =
    [PayrollRunStatus.Draft, PayrollRunStatus.Calculated, PayrollRunStatus.Reviewed, PayrollRunStatus.Approved];

  public async Task<Result<GetPayrollPeriodsQueryResult>> Handle(GetPayrollPeriodsQuery query, CancellationToken cancellationToken)
  {
    var periods = await context.PayrollPeriods.AsNoTracking().Where(p => query.Year == null || p.Year == query.Year)
      .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync(cancellationToken);
    var ids = periods.Select(p => p.Id).ToList();
    var runs = (await context.PayrollRuns.AsNoTracking().Where(r => ids.Contains(r.PayrollPeriodId)).Select(r => r.PayrollPeriodId).ToListAsync(cancellationToken))
      .GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());

    return Result<GetPayrollPeriodsQueryResult>.Success(new(periods.Select(p =>
      new PayrollPeriodDto(p.Id.Value, p.Year, p.Month, p.Label, p.StartDate, p.EndDate, p.Status, runs.GetValueOrDefault(p.Id))).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePayrollPeriodCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("open a payroll month", cancellationToken) is { } refusal)
      return Result<CreatedResult>.Failure(refusal);
    if (await context.PayrollPeriods.AnyAsync(p => p.Year == command.Year && p.Month == command.Month, cancellationToken))
      return Result<CreatedResult>.Failure($"{new DateOnly(command.Year, command.Month, 1):MMMM yyyy} already exists.");

    var period = PayrollPeriod.Create(PayrollPeriodId.New(), command.Year, command.Month, command.StartDate, command.EndDate);
    if (await OverlapAsync(period.Id, period.StartDate, period.EndDate, cancellationToken) is { } clash)
      return Result<CreatedResult>.Failure(clash);
    context.PayrollPeriods.Add(period);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(period.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(ChangePayrollPeriodStatusCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("close, reopen or lock a payroll month", cancellationToken) is { } refusal)
      return Result<UpdatedResult>.Failure(refusal);
    var period = await context.LoadPayrollPeriodAsync(command.Id, cancellationToken);
    switch (command.Action)
    {
      case PayrollPeriodAction.Close:
        period.Close(await context.PayrollRuns.CountAsync(r => r.PayrollPeriodId == period.Id && Unfinished.Contains(r.Status), cancellationToken));
        break;
      case PayrollPeriodAction.Reopen:
        period.Reopen();
        break;
      case PayrollPeriodAction.Lock:
        period.Lock();
        break;
    }

    audit.Record(command.Action switch
    {
      PayrollPeriodAction.Close => HrmVerbs.Closed,
      PayrollPeriodAction.Reopen => HrmVerbs.Reopened,
      _ => HrmVerbs.Locked
    }, period);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePayrollPeriodDatesCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("change a payroll month's dates", cancellationToken) is { } refusal)
      return Result<UpdatedResult>.Failure(refusal);
    var period = await context.LoadPayrollPeriodAsync(command.Id, cancellationToken);
    period.ChangeDates(command.StartDate, command.EndDate, await context.PayrollRuns.CountAsync(r => r.PayrollPeriodId == period.Id, cancellationToken));
    if (await OverlapAsync(period.Id, period.StartDate, period.EndDate, cancellationToken) is { } clash)
      return Result<UpdatedResult>.Failure(clash);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeletePayrollPeriodCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("delete a payroll month", cancellationToken) is { } refusal)
      return Result<UpdatedResult>.Failure(refusal);
    var period = await context.LoadPayrollPeriodAsync(command.Id, cancellationToken);
    period.EnsureUnused(await context.PayrollRuns.CountAsync(r => r.PayrollPeriodId == period.Id, cancellationToken), "be deleted");
    context.PayrollPeriods.Remove(period);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// A day can belong to one payroll month only.
  private async Task<string?> OverlapAsync(PayrollPeriodId self, DateOnly start, DateOnly end, CancellationToken cancellationToken)
  {
    var other = await context.PayrollPeriods.AsNoTracking()
      .Where(p => p.Id != self && p.StartDate <= end && start <= p.EndDate)
      .OrderBy(p => p.StartDate).FirstOrDefaultAsync(cancellationToken);
    return other is null ? null : $"{start:d MMM yyyy} to {end:d MMM yyyy} overlaps {other.Label} ({other.StartDate:d MMM yyyy} to {other.EndDate:d MMM yyyy}).";
  }
}
