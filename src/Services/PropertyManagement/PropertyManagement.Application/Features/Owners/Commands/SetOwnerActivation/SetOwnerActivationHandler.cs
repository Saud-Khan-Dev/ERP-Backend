using Microsoft.EntityFrameworkCore;

public class SetOwnerActivationHandler(IApplicationDbContext context)
  : ICommandHandler<SetOwnerActivationCommand, Result<SetOwnerActivationCommandResult>>
{
  public async Task<Result<SetOwnerActivationCommandResult>> Handle(SetOwnerActivationCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.Id, cancellationToken);

    if (command.IsActive)
      owner.Activate();
    else
      owner.Deactivate(await CommitmentsAsync(owner.Id, cancellationToken));

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetOwnerActivationCommandResult>.Success(new SetOwnerActivationCommandResult(owner.IsActive));
  }

  /// What still ties the owner to an active property (records on deactivated properties no longer count):
  /// a current share (active or disputed), a pending transfer (initiated or approved), or an allotment, lease,
  /// rental or contract in force (a lease still in draft too: it names the owner as lessee).
  private async Task<PropertyOwner.Commitments> CommitmentsAsync(OwnerId ownerId, CancellationToken cancellationToken)
  {
    var activeProperties = context.Properties.AsNoTracking().Where(p => p.IsActive).Select(p => p.Id);
    var held = await context.Ownerships.AsNoTracking()
        .Where(o => o.OwnerId == ownerId
          && (o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Disputed)
          && activeProperties.Contains(o.PropertyId))
        .ToListAsync(cancellationToken);

    var heldIds = held.Select(o => o.PropertyId).Distinct().ToList();
    var codes = (await context.Properties.AsNoTracking().Where(p => heldIds.Contains(p.Id)).Select(p => new { p.Id, p.PropertyCode }).ToListAsync(cancellationToken))
      .ToDictionary(p => p.Id, p => p.PropertyCode.Value);

    var shares = held
      .Select(o => (Code: codes[o.PropertyId], Label: $"{codes[o.PropertyId]} ({o.OwnershipSharePct.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}%"
        + (o.OwnershipStatus == OwnershipStatus.Disputed ? ", disputed)" : ")")))
      .OrderBy(x => x.Code, StringComparer.Ordinal)
      .Select(x => x.Label)
      .ToList();

    var transfers = (await context.Transfers.AsNoTracking()
        .Where(t => (t.TransferStatus == TransferStatus.Initiated || t.TransferStatus == TransferStatus.Approved)
          && t.Parties.Any(p => p.OwnerId == ownerId)
          && activeProperties.Contains(t.PropertyId))
        .Select(t => t.TransferNo)
        .ToListAsync(cancellationToken))
      .Select(c => c.Value).Order(StringComparer.Ordinal).ToList();

    var allotmentStatuses = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
    var leaseStatuses = await InForce.StatusIdsAsync<LeaseStatus>(context, [.. InForce.LeaseStatuses, SystemMasterCodes.Draft], cancellationToken);
    var draftLease = await InForce.StatusIdsAsync<LeaseStatus>(context, [SystemMasterCodes.Draft], cancellationToken);
    var rentalStatuses = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
    var contractStatuses = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);

    var agreements = new List<string>();

    agreements.AddRange((await context.Allotments.AsNoTracking()
        .Where(x => x.AllotteeOwnerId == ownerId && x.IsActive && allotmentStatuses.Contains(x.AllotmentStatusId) && activeProperties.Contains(x.PropertyId))
        .Select(x => x.AllotmentNo).ToListAsync(cancellationToken)).Select(c => c.Value).Order(StringComparer.Ordinal));

    agreements.AddRange((await context.Leases.AsNoTracking()
        .Where(x => x.LesseeOwnerId == ownerId && leaseStatuses.Contains(x.LeaseStatusId) && activeProperties.Contains(x.PropertyId))
        .Select(x => new { x.LeaseNo, x.LeaseStatusId }).ToListAsync(cancellationToken))
      .Select(x => draftLease.Contains(x.LeaseStatusId) ? $"{x.LeaseNo.Value} (draft)" : x.LeaseNo.Value).Order(StringComparer.Ordinal));

    agreements.AddRange((await context.Rentals.AsNoTracking()
        .Where(x => x.TenantOwnerId == ownerId && rentalStatuses.Contains(x.RentalStatusId) && activeProperties.Contains(x.PropertyId))
        .Select(x => x.RentalNo).ToListAsync(cancellationToken)).Select(c => c.Value).Order(StringComparer.Ordinal));

    agreements.AddRange((await context.Outsourcings.AsNoTracking()
        .Where(x => x.OutsourcedPartyOwnerId == ownerId && contractStatuses.Contains(x.ContractStatusId) && activeProperties.Contains(x.PropertyId))
        .Select(x => x.ContractNo).ToListAsync(cancellationToken)).Select(c => c.Value).Order(StringComparer.Ordinal));

    return new PropertyOwner.Commitments(shares, transfers, agreements);
  }
}
