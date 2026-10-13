public class ConfirmAllotmentOwnershipHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<ConfirmAllotmentOwnershipCommand, Result<ConfirmAllotmentOwnershipCommandResult>>
{
  public async Task<Result<ConfirmAllotmentOwnershipCommandResult>> Handle(ConfirmAllotmentOwnershipCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.AllotmentId, cancellationToken);
    var status = await masters.GetAsync<AllotmentStatus>(allotment.AllotmentStatusId.Value, cancellationToken);
    allotment.EnsureCanConfirmOwnership(status);

    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    var allottee = await context.LoadOwnerAsync(allotment.AllotteeOwnerId.Value, cancellationToken);
    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, new[] { allottee.Id }, cancellationToken);

    // in force, not confirmed yet, dated between the allotment (or restoration) and today, rule 1
    var ownership = allotment.ConfirmOwnership(
      OwnershipId.New(), status, allottee, tenure, command.OwnershipSharePct, command.EffectiveFrom,
      command.ReferenceNo, command.Remarks, record);

    await context.Ownerships.AddAsync(ownership, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ConfirmAllotmentOwnershipCommandResult>.Success(new ConfirmAllotmentOwnershipCommandResult(ownership.Id.Value));
  }
}
