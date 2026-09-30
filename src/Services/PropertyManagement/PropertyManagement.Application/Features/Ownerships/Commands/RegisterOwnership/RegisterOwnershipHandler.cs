public class RegisterOwnershipHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<RegisterOwnershipCommand, Result<RegisterOwnershipCommandResult>>
{
  public async Task<Result<RegisterOwnershipCommandResult>> Handle(RegisterOwnershipCommand command, CancellationToken cancellationToken)
  {
    var input = command.Ownership;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var owner = await context.LoadOwnerAsync(input.OwnerId, cancellationToken);
    var tenure = await masters.GetAsync<TenureType>(input.TenureTypeId, cancellationToken);
    var acquiredBy = await masters.GetOptionalAsync<TransferType>(input.AcquisitionTransferTypeId, cancellationToken);

    var current = await context.CurrentOwnershipsAsync(property.Id, cancellationToken);

    if (current.Any(o => o.OwnerId == owner.Id))
      return Result<RegisterOwnershipCommandResult>.Failure(
        $"{owner.OwnerName.Value} already holds a current share of {property.PropertyCode.Value}. Record a transfer to change it.");

    // rule 1: active shares may total at most 100%
    OwnershipShares.EnsureRoomFor(current, input.OwnershipSharePct);

    var ownership = PropertyOwnership.Register(
      OwnershipId.New(), property, owner, tenure, input.OwnershipSharePct, input.EffectiveFrom,
      acquiredBy, input.ReferenceNo, input.Remarks);

    await context.Ownerships.AddAsync(ownership, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RegisterOwnershipCommandResult>.Success(new RegisterOwnershipCommandResult(ownership.Id.Value));
  }
}
