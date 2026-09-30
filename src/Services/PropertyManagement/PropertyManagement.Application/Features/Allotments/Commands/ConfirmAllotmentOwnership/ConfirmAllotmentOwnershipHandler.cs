public class ConfirmAllotmentOwnershipHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<ConfirmAllotmentOwnershipCommand, Result<ConfirmAllotmentOwnershipCommandResult>>
{
  public async Task<Result<ConfirmAllotmentOwnershipCommandResult>> Handle(ConfirmAllotmentOwnershipCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.AllotmentId, cancellationToken);
    allotment.EnsureCanConfirmOwnership(await masters.GetAsync<AllotmentStatus>(allotment.AllotmentStatusId.Value, cancellationToken));

    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    var allottee = await context.LoadOwnerAsync(allotment.AllotteeOwnerId.Value, cancellationToken);

    var tenure = command.TenureTypeId is { } tenureId
      ? await masters.GetAsync<TenureType>(tenureId, cancellationToken)
      : await masters.GetByCodeAsync<TenureType>(SystemMasterCodes.TenureOwned, cancellationToken);

    var current = await context.CurrentOwnershipsAsync(property.Id, cancellationToken);

    if (current.Any(o => o.OwnerId == allottee.Id))
      return Result<ConfirmAllotmentOwnershipCommandResult>.Failure(
        $"{allottee.OwnerName.Value} already holds a current share of {property.PropertyCode.Value}.");

    // rule 1
    OwnershipShares.EnsureRoomFor(current, command.OwnershipSharePct);

    var ownership = PropertyOwnership.FromAllotment(
      OwnershipId.New(), property, allotment, allottee, tenure, command.OwnershipSharePct, command.EffectiveFrom,
      command.ReferenceNo ?? allotment.AllotmentLetterRef, command.Remarks ?? $"Confirmed from allotment {allotment.AllotmentNo.Value}.");

    await context.Ownerships.AddAsync(ownership, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ConfirmAllotmentOwnershipCommandResult>.Success(new ConfirmAllotmentOwnershipCommandResult(ownership.Id.Value));
  }
}
