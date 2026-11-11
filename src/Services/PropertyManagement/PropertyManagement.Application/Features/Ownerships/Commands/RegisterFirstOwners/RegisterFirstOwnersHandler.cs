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

    // the clerk enters a property nobody has held yet from the paper register; one that was held before (its shares
    // ended with a cancelled allotment, say) gets owners only from an approver
    if (record.Current.Count == 0 && record.HasHolderHistory && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException(
        $"{property.PropertyCode.Value} had owners before, so only an approver can enter its owners now. Ask an officer who can approve.");

    var rows = PropertyOwnership.RegisterFirstOwners(owners, record);

    await context.Ownerships.AddRangeAsync(rows, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RegisterFirstOwnersCommandResult>.Success(new RegisterFirstOwnersCommandResult(rows.Select(r => r.Id.Value).ToList()));
  }
}
