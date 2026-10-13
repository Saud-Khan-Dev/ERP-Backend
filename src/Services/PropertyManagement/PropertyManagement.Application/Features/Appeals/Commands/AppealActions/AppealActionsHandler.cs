using Microsoft.EntityFrameworkCore;

public class AppealActionsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateAppealCommand, Result<AppealActionResult>>,
    ICommandHandler<StartAppealHearingCommand, Result<AppealActionResult>>,
    ICommandHandler<DecideAppealCommand, Result<AppealActionResult>>,
    ICommandHandler<WithdrawAppealCommand, Result<AppealActionResult>>
{
  public async Task<Result<AppealActionResult>> Handle(UpdateAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(appeal.PropertyId, cancellationToken);

    var otherAppeals = await context.Appeals.AsNoTracking()
        .Where(a => a.PropertyId == appeal.PropertyId && a.Id != appeal.Id).ToListAsync(cancellationToken);

    var warnings = appeal.Update(await command.Appeal.ToFilingAsync(context, cancellationToken), otherAppeals);
    return await SaveAsync(appeal, warnings, Array.Empty<string>(), cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(StartAppealHearingCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(appeal.PropertyId, cancellationToken);
    appeal.StartHearing();
    return await SaveAsync(appeal, Array.Empty<string>(), Array.Empty<string>(), cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(DecideAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(appeal.PropertyId, cancellationToken);
    appeal.Decide(command.DecisionDate, command.DecisionOutcome, command.DecisionDetails);

    var warnings = command.DecisionDate > appeal.DecisionDueDate
      ? new[] { $"Decided {command.DecisionDate.DayNumber - appeal.DecisionDueDate.DayNumber} days after the {PropertyAppeal.DecisionWindowDays}-day limit of Act s.32(1)." }
      : Array.Empty<string>();

    var effects = await ApplyToAppealedRecordAsync(appeal, cancellationToken);
    return await SaveAsync(appeal, warnings, effects, cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(WithdrawAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(appeal.PropertyId, cancellationToken);
    appeal.Withdraw(command.Remarks);

    var effects = await ApplyToAppealedRecordAsync(appeal, cancellationToken);
    return await SaveAsync(appeal, Array.Empty<string>(), effects, cancellationToken);
  }

  /// A closed appeal against a violation moves the violation out of APPEALED: set aside when the appeal is
  /// allowed (and a lease / rental it cancelled is reinstated), otherwise back to its status before the appeal.
  private async Task<IReadOnlyList<string>> ApplyToAppealedRecordAsync(PropertyAppeal appeal, CancellationToken cancellationToken)
  {
    if (appeal.OrderSourceTable != AppealOrderSource.AgreementViolation || appeal.OrderSourceId is not { } violationId)
      return Array.Empty<string>();

    var violation = await context.LoadViolationAsync(violationId, cancellationToken);
    var before = violation.ViolationStatus;
    var reinstate = violation.ApplyAppealOutcome(appeal);
    var effects = new List<string>();

    if (violation.ViolationStatus != before)
      effects.Add(violation.ViolationStatus == ViolationStatus.Rectified
        ? $"Violation no. {violation.OccurrenceNo} of {violation.ViolationDate:yyyy-MM-dd} is set aside{(violation.FineStatus == FineStatus.Waived ? " and its fine waived" : "")}."
        : $"Violation no. {violation.OccurrenceNo} of {violation.ViolationDate:yyyy-MM-dd} is back to {violation.ViolationStatus}.");

    if (!reinstate)
      return effects;

    var reason = appeal.AppealNo.Value;

    // the decision stands even if the agreement has since moved on; the clerk is told it was not reinstated
    if (violation.LeaseId is { } leaseId)
    {
      var lease = await context.LoadLeaseAsync(leaseId.Value, cancellationToken);
      var status = await masters.GetAsync<LeaseStatus>(lease.LeaseStatusId.Value, cancellationToken);

      if (status.Is(SystemMasterCodes.Cancelled))
      {
        lease.Reinstate(status, await masters.GetByCodeAsync<LeaseStatus>(SystemMasterCodes.Active, cancellationToken), reason);
        effects.Add($"Lease {lease.LeaseNo.Value} is reinstated (ACTIVE again).");
      }
      else
        effects.Add($"Lease {lease.LeaseNo.Value} is {status.Name.Value}, not cancelled, so it was not reinstated.");
    }

    if (violation.RentalId is { } rentalId)
    {
      var rental = await context.LoadRentalAsync(rentalId.Value, cancellationToken);
      var status = await masters.GetAsync<RentalStatus>(rental.RentalStatusId.Value, cancellationToken);

      if (status.Is(SystemMasterCodes.Cancelled))
      {
        rental.Reinstate(status, await masters.GetByCodeAsync<RentalStatus>(SystemMasterCodes.Active, cancellationToken), reason);
        effects.Add($"Rental {rental.RentalNo.Value} is reinstated (ACTIVE again).");
      }
      else
        effects.Add($"Rental {rental.RentalNo.Value} is {status.Name.Value}, not cancelled, so it was not reinstated.");
    }

    return effects;
  }

  private async Task<Result<AppealActionResult>> SaveAsync(PropertyAppeal appeal, IReadOnlyList<string> warnings, IReadOnlyList<string> effects, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    return Result<AppealActionResult>.Success(new AppealActionResult(appeal.Id.Value, appeal.AppealStatus, appeal.DecisionDueDate, warnings, effects));
  }
}
