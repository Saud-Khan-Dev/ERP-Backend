public class CreateLeaseHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateLeaseCommand, Result<CreateLeaseCommandResult>>
{
  public async Task<Result<CreateLeaseCommandResult>> Handle(CreateLeaseCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var lessee = await context.LoadOwnerAsync(command.LesseeOwnerId, cancellationToken);
    var status = await masters.GetByCodeAsync<LeaseStatus>(command.AsDraft ? SystemMasterCodes.Draft : SystemMasterCodes.Active, cancellationToken);

    var lease = PropertyLease.Create(
      LeaseId.New(), property, lessee, await codes.NextAsync(CodeSequenceKeys.Lease, cancellationToken), status,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.Leases.AddAsync(lease, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateLeaseCommandResult>.Success(new CreateLeaseCommandResult(lease.Id.Value, lease.LeaseNo.Value));
  }
}
