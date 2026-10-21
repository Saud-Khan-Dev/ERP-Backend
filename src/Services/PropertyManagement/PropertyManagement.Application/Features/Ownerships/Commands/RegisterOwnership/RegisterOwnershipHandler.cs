public class RegisterOwnershipHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<RegisterOwnershipCommand, Result<RegisterOwnershipCommandResult>>
{
  public async Task<Result<RegisterOwnershipCommandResult>> Handle(RegisterOwnershipCommand command, CancellationToken cancellationToken)
  {
    var input = command.Ownership;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var owner = await context.LoadOwnerAsync(input.OwnerId, cancellationToken);
    var tenure = await masters.GetAsync<TenureType>(input.TenureTypeId, cancellationToken);
    var acquiredBy = await masters.GetOptionalAsync<TransferType>(input.AcquisitionTransferTypeId, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, new[] { owner.Id }, cancellationToken);

    // a property's first holders may be entered from the paper register by the clerk; once anyone has held it,
    // a new owner changes who holds the land, which is an approver's act
    if (record.HasHolderHistory && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException(
        $"{property.PropertyCode.Value} already has holders on record, so adding an owner is an approver's act. "
        + "Record a transfer instead, or ask an officer who can approve.");

    // one current share per owner, rule 1 (at most 100% held), no future start, no overlap with the owner's history,
    // never while an allotment is in force
    var ownership = PropertyOwnership.AddOwner(
      OwnershipId.New(), owner, tenure, input.OwnershipSharePct, input.EffectiveFrom,
      acquiredBy, input.ReferenceNo, input.Remarks, record);

    await context.Ownerships.AddAsync(ownership, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RegisterOwnershipCommandResult>.Success(new RegisterOwnershipCommandResult(ownership.Id.Value));
  }
}
