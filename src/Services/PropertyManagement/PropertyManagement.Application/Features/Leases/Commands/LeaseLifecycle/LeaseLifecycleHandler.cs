public class LeaseLifecycleHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<ActivateLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<ExpireLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<TerminateLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<RenewLeaseCommand, Result<RenewLeaseCommandResult>>
{
  public async Task<Result<LeaseLifecycleResult>> Handle(ActivateLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    lease.Activate(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Active, cancellationToken));
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<LeaseLifecycleResult>> Handle(ExpireLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    lease.Expire(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Expired, cancellationToken));
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<LeaseLifecycleResult>> Handle(TerminateLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    lease.Terminate(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Terminated, cancellationToken),
      command.TerminationDate, command.Reason);
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<RenewLeaseCommandResult>> Handle(RenewLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    var lessee = await context.LoadOwnerAsync(command.LesseeOwnerId ?? lease.LesseeOwnerId.Value, cancellationToken);

    var renewal = lease.Renew(
      await Current(lease, cancellationToken),
      await Status(SystemMasterCodes.Renewed, cancellationToken),
      await Status(SystemMasterCodes.Active, cancellationToken),
      LeaseId.New(),
      await codes.NextAsync(CodeSequenceKeys.Lease, cancellationToken),
      lessee,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.Leases.AddAsync(renewal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RenewLeaseCommandResult>.Success(new RenewLeaseCommandResult(renewal.Id.Value, renewal.LeaseNo.Value, lease.Id.Value));
  }

  private Task<LeaseStatus> Current(PropertyLease lease, CancellationToken cancellationToken) =>
      masters.GetAsync<LeaseStatus>(lease.LeaseStatusId.Value, cancellationToken);

  private Task<LeaseStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<LeaseStatus>(code, cancellationToken);

  private async Task<Result<LeaseLifecycleResult>> SaveAsync(PropertyLease lease, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<LeaseStatus>(lease.LeaseStatusId).LoadAsync(cancellationToken);
    return Result<LeaseLifecycleResult>.Success(new LeaseLifecycleResult(lease.Id.Value, refs[lease.LeaseStatusId]));
  }
}
