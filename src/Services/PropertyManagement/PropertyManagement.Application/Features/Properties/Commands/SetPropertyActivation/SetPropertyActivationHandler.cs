using Microsoft.EntityFrameworkCore;

public class SetPropertyActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetPropertyActivationCommand, Result<SetPropertyActivationCommandResult>>
{
  public async Task<Result<SetPropertyActivationCommandResult>> Handle(SetPropertyActivationCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.Id, cancellationToken);

    if (command.IsActive)
    {
      property.Activate();
    }
    else
    {
      // a property taken off the register may not leave anything running on it, nor anyone holding it
      var open = await OpenRecordsAsync(property.Id, cancellationToken);
      if (open.Count > 0)
        throw new DomainException($"{property.PropertyCode.Value} cannot be deactivated while records on it are still open. End or cancel these first: {string.Join(", ", open)}.");

      property.Deactivate(command.Reason, PropertyWriteGuard.Today());
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetPropertyActivationCommandResult>.Success(new SetPropertyActivationCommandResult(property.IsActive));
  }

  /// Current holders, pending transfers, charges in force, the allotment in force, active or draft leases / rentals /
  /// contracts, running auctions, building plans under consideration and open legal matters (encroachments, court
  /// cases, appeals, violations — the rules of OpenMatters), by code.
  private async Task<List<string>> OpenRecordsAsync(PropertyId propertyId, CancellationToken cancellationToken)
  {
    var open = new List<string>();

    var holders = await context.CurrentOwnershipsAsync(propertyId, cancellationToken);
    if (holders.Count > 0)
    {
      var ownerIds = holders.Select(h => h.OwnerId).Distinct().ToList();
      var names = await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OwnerName.Value, cancellationToken);
      open.AddRange(holders.GroupBy(h => h.OwnerId)
        .Select(g => $"the {g.Sum(h => h.OwnershipSharePct):0.####}% share held by {names.GetValueOrDefault(g.Key, "an owner")}"));
    }

    open.AddRange((await context.Encumbrances.AsNoTracking()
        .Where(e => e.PropertyId == propertyId && (e.Status == EncumbranceStatus.Active || e.Status == EncumbranceStatus.Enforced))
        .Select(e => new { e.HolderName, e.ReferenceNo }).ToListAsync(cancellationToken))
      .Select(e => $"the charge held by {e.HolderName}{(e.ReferenceNo is null ? "" : $" (ref {e.ReferenceNo})")}"));

    open.AddRange((await context.Transfers.AsNoTracking()
        .Where(t => t.PropertyId == propertyId && (t.TransferStatus == TransferStatus.Initiated || t.TransferStatus == TransferStatus.Approved))
        .Select(t => t.TransferNo).ToListAsync(cancellationToken))
      .Select(c => $"transfer {c.Value}"));

    var allotmentsInForce = await context.InForceAllotmentStatusIdsAsync(cancellationToken);
    open.AddRange((await context.Allotments.AsNoTracking()
        .Where(a => a.PropertyId == propertyId && a.IsActive && allotmentsInForce.Contains(a.AllotmentStatusId))
        .Select(a => a.AllotmentNo).ToListAsync(cancellationToken))
      .Select(c => $"allotment {c.Value}"));

    var activeLease = await LiveStatusIdsAsync<LeaseStatus>(cancellationToken);
    open.AddRange((await context.Leases.AsNoTracking()
        .Where(l => l.PropertyId == propertyId && activeLease.Contains(l.LeaseStatusId))
        .Select(l => l.LeaseNo).ToListAsync(cancellationToken))
      .Select(c => $"lease {c.Value}"));

    var activeRental = await LiveStatusIdsAsync<RentalStatus>(cancellationToken);
    open.AddRange((await context.Rentals.AsNoTracking()
        .Where(r => r.PropertyId == propertyId && activeRental.Contains(r.RentalStatusId))
        .Select(r => r.RentalNo).ToListAsync(cancellationToken))
      .Select(c => $"rental {c.Value}"));

    var activeContract = await LiveStatusIdsAsync<ContractStatus>(cancellationToken);
    open.AddRange((await context.Outsourcings.AsNoTracking()
        .Where(o => o.PropertyId == propertyId && activeContract.Contains(o.ContractStatusId))
        .Select(o => o.ContractNo).ToListAsync(cancellationToken))
      .Select(c => $"contract {c.Value}"));

    var runningAuction = await InForce.StatusIdsAsync<AuctionStatus>(context, InForce.AuctionStatuses, cancellationToken);
    open.AddRange((await context.Auctions.AsNoTracking()
        .Where(a => a.PropertyId == propertyId && runningAuction.Contains(a.AuctionStatusId))
        .Select(a => a.AuctionNo).ToListAsync(cancellationToken))
      .Select(c => $"auction {c.Value}"));

    var decidedPlan = await InForce.StatusIdsAsync<BuildingPlanStatus>(context, BuildingPlan.ClosedStatusCodes, cancellationToken);
    open.AddRange((await context.BuildingPlans.AsNoTracking()
        .Where(b => b.PropertyId == propertyId && !decidedPlan.Contains(b.BuildingPlanStatusId))
        .Select(b => new { b.PlanNo, b.RevisionNo }).ToListAsync(cancellationToken))
      .Select(b => $"building plan {b.PlanNo.Value} rev. {b.RevisionNo}"));

    open.AddRange((await context.Encroachments.AsNoTracking()
        .Where(e => e.PropertyId == propertyId).Where(OpenMatters.EncroachmentIsOpen)
        .Select(e => e.EncroachmentNo).ToListAsync(cancellationToken))
      .Select(c => $"encroachment {c.Value}"));

    var closedCase = await OpenMatters.ClosedCaseStatusIdsAsync(context, cancellationToken);
    open.AddRange((await context.Litigations.AsNoTracking()
        .Where(l => l.PropertyId == propertyId).Where(OpenMatters.CaseIsOpen(closedCase))
        .Select(l => l.CaseNo).ToListAsync(cancellationToken))
      .Select(c => $"court case {c}"));

    open.AddRange((await context.Appeals.AsNoTracking()
        .Where(a => a.PropertyId == propertyId).Where(OpenMatters.AppealIsOpen)
        .Select(a => a.AppealNo).ToListAsync(cancellationToken))
      .Select(c => $"appeal {c.Value}"));

    open.AddRange((await context.Violations.AsNoTracking()
        .Where(v => v.PropertyId == propertyId).Where(OpenMatters.ViolationIsOpen)
        .Select(v => new { v.NoticeNo, v.ViolationDate }).ToListAsync(cancellationToken))
      .Select(v => v.NoticeNo is null ? $"violation of {v.ViolationDate:yyyy-MM-dd}" : $"violation notice {v.NoticeNo}"));

    return open;
  }

  /// ACTIVE, and DRAFT (an agreement still being drawn up is a record running on the property too).
  private async Task<List<MasterId>> LiveStatusIdsAsync<T>(CancellationToken cancellationToken) where T : MasterData =>
      (await context.Set<T>().AsNoTracking().ToListAsync(cancellationToken))
        .Where(s => s.Is(SystemMasterCodes.Active) || s.Is(SystemMasterCodes.Draft))
        .Select(s => s.Id)
        .ToList();
}
