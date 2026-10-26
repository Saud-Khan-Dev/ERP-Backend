public class RegisterFirstOwnersHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<RegisterFirstOwnersCommand, Result<RegisterFirstOwnersCommandResult>>
{
  public async Task<Result<RegisterFirstOwnersCommandResult>> Handle(RegisterFirstOwnersCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var owners = new List<PropertyOwnership.FirstOwner>();
    foreach (var input in command.Owners)
      owners.Add(new PropertyOwnership.FirstOwner(
        await context.LoadOwnerAsync(input.OwnerId, cancellationToken),
        await masters.GetAsync<TenureType>(input.TenureTypeId, cancellationToken),
        input.OwnershipSharePct,
        input.EffectiveFrom,
        await masters.GetOptionalAsync<TransferType>(input.AcquisitionTransferTypeId, cancellationToken),
        input.ReferenceNo,
        input.Remarks));

    var record = await context.LoadOwnershipRecordAsync(property, owners.Select(o => o.Owner.Id), cancellationToken);

    // a held property is refused by the domain (a transfer brings a new owner in) whoever asks; an ownerless one that
    // was held before (its shares ended through an allotment's cancellation, say) gets owners only from an approver -
    // the clerk enters the first owners of a property nobody has held yet, from the paper register
    if (record.Current.Count == 0 && record.HasHolderHistory && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException(
        $"{property.PropertyCode.Value} was held before, so entering its owners now is an approver's act. Ask an officer who can approve.");

    // nobody holds it now, no allotment in force, exactly 100% between them, each owner once, no future or overlapping date
    var rows = PropertyOwnership.RegisterFirstOwners(owners, record);

    await context.Ownerships.AddRangeAsync(rows, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RegisterFirstOwnersCommandResult>.Success(new RegisterFirstOwnersCommandResult(rows.Select(r => r.Id.Value).ToList()));
  }
}
