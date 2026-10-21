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
    appeal.Decide(command.DecisionDate, command.DecisionOutcome, command.DecisionDetails, PropertyWriteGuard.Today());

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

  /// What a closed appeal does to the record whose order it was against:
  ///   violation  — out of APPEALED: set aside when the appeal is allowed (and a lease / rental it cancelled is
  ///                reinstated), otherwise back to its status before the appeal;
  ///   allotment  — allowed against its cancellation: the allotment is restored on the decision date (unless another
  ///                allotment is in force by now, which the clerk is told);
  ///   building plan — allowed against its rejection: the plan is under consideration again.
  private async Task<IReadOnlyList<string>> ApplyToAppealedRecordAsync(PropertyAppeal appeal, CancellationToken cancellationToken)
  {
    var allowed = appeal.AppealStatus == AppealStatus.Decided && appeal.DecisionOutcome == AppealOutcome.Allowed;

    if (allowed && appeal.OrderSourceTable == AppealOrderSource.PropertyAllotment && appeal.OrderSourceId is { } allotmentId)
      return await RestoreAllotmentAsync(appeal, allotmentId, cancellationToken);

    if (allowed && appeal.OrderSourceTable == AppealOrderSource.BuildingPlan && appeal.OrderSourceId is { } planId)
      return await ReopenPlanAsync(appeal, planId, cancellationToken);

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

  private async Task<IReadOnlyList<string>> RestoreAllotmentAsync(PropertyAppeal appeal, Guid allotmentId, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(allotmentId, cancellationToken);
    var status = await masters.GetAsync<AllotmentStatus>(allotment.AllotmentStatusId.Value, cancellationToken);

    if (!status.Is(SystemMasterCodes.Cancelled) || !allotment.IsActive)
      return new[] { $"Allotment {allotment.AllotmentNo.Value} is {status.Name.Value.ToLowerInvariant()}, not cancelled, so the appeal had nothing to restore." };

    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    var other = await context.AllotmentInForceAsync(property.Id, allotment.Id, cancellationToken);
    if (other is not null)
      return new[] { $"Allotment {allotment.AllotmentNo.Value} was not restored: {other.Allotment.AllotmentNo.Value} to {other.AllotteeName} is in force on the plot now. Settle that first, then restore it." };

    context.Touch(property);
    var restoredOn = appeal.DecisionDate ?? PropertyWriteGuard.Today();
    allotment.Restore(property, status, await masters.GetByCodeAsync<AllotmentStatus>(SystemMasterCodes.Restored, cancellationToken),
      restoredOn < (allotment.CancellationDate ?? restoredOn) ? allotment.CancellationDate!.Value : restoredOn,
      appeal.AppealNo.Value, other, PropertyWriteGuard.Today());

    return new[] { $"Allotment {allotment.AllotmentNo.Value} is restored by this decision; confirm the allottee as owner again if they had been." };
  }

  private async Task<IReadOnlyList<string>> ReopenPlanAsync(PropertyAppeal appeal, Guid planId, CancellationToken cancellationToken)
  {
    var plan = await context.LoadBuildingPlanAsync(planId, cancellationToken);
    var status = await masters.GetAsync<BuildingPlanStatus>(plan.BuildingPlanStatusId.Value, cancellationToken);

    if (!status.Is(SystemMasterCodes.Rejected))
      return new[] { $"Plan {plan.PlanNo.Value} rev. {plan.RevisionNo} is {status.Name.Value.ToLowerInvariant()}, not rejected, so the appeal had nothing to reopen." };

    plan.ReopenOnAppeal(status, await masters.GetByCodeAsync<BuildingPlanStatus>(SystemMasterCodes.Submitted, cancellationToken), appeal.AppealNo.Value);
    return new[] { $"Plan {plan.PlanNo.Value} rev. {plan.RevisionNo} is under consideration again." };
  }

  private async Task<Result<AppealActionResult>> SaveAsync(PropertyAppeal appeal, IReadOnlyList<string> warnings, IReadOnlyList<string> effects, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    return Result<AppealActionResult>.Success(new AppealActionResult(appeal.Id.Value, appeal.AppealStatus, appeal.DecisionDueDate, warnings, effects));
  }
}
