public class LeaseLifecycleHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, IAuditTrail audit)
  : ICommandHandler<ActivateLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<ExpireLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<TerminateLeaseCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<WithdrawLeaseDraftCommand, Result<LeaseLifecycleResult>>,
    ICommandHandler<RenewLeaseCommand, Result<RenewLeaseCommandResult>>
{
  public async Task<Result<LeaseLifecycleResult>> Handle(ActivateLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(lease.PropertyId, cancellationToken);
    lease.Activate(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Active, cancellationToken));

    // the draft comes into force: no other lease may run at the same time
    context.Touch(property);
    var lessee = await context.LoadOwnerAsync(lease.LesseeOwnerId.Value, cancellationToken);
    var warnings = AgreementOverlap.Check(lease.Span(lessee.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken));
    return await SaveAsync(lease, cancellationToken, warnings);
  }

  public async Task<Result<LeaseLifecycleResult>> Handle(ExpireLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(lease.PropertyId, cancellationToken);
    lease.Expire(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Expired, cancellationToken), PropertyWriteGuard.Today());
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<LeaseLifecycleResult>> Handle(TerminateLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(lease.PropertyId, cancellationToken);
    lease.Terminate(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Terminated, cancellationToken),
      command.TerminationDate, command.Reason, PropertyWriteGuard.Today());
    audit.Record(PropertyAuditActions.Terminated, lease, command.Reason);
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<LeaseLifecycleResult>> Handle(WithdrawLeaseDraftCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(lease.PropertyId, cancellationToken);
    lease.WithdrawDraft(await Current(lease, cancellationToken), await Status(SystemMasterCodes.Withdrawn, cancellationToken), command.Reason);
    return await SaveAsync(lease, cancellationToken);
  }

  public async Task<Result<RenewLeaseCommandResult>> Handle(RenewLeaseCommand command, CancellationToken cancellationToken)
  {
    var lease = await context.LoadLeaseAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(lease.PropertyId, cancellationToken);
    var lessee = await context.LoadOwnerAsync(command.LesseeOwnerId ?? lease.LesseeOwnerId.Value, cancellationToken);

    var renewal = lease.Renew(
      await Current(lease, cancellationToken),
      await Status(SystemMasterCodes.Renewed, cancellationToken),
      await Status(SystemMasterCodes.Active, cancellationToken),
      LeaseId.New(),
      await codes.NextAsync(CodeSequenceKeys.Lease, cancellationToken),
      lessee,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    // the renewal replaces this lease: it may not overlap any other lease
    context.Touch(property);
    var warnings = new List<string>(renewal.TermWarnings());
    warnings.AddRange(AgreementOverlap.Check(renewal.Span(lessee.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken), except: lease.LeaseNo.Value));

    await context.Leases.AddAsync(renewal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RenewLeaseCommandResult>.Success(new RenewLeaseCommandResult(renewal.Id.Value, renewal.LeaseNo.Value, lease.Id.Value, warnings));
  }

  private Task<LeaseStatus> Current(PropertyLease lease, CancellationToken cancellationToken) =>
      masters.GetAsync<LeaseStatus>(lease.LeaseStatusId.Value, cancellationToken);

  private Task<LeaseStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<LeaseStatus>(code, cancellationToken);

  private async Task<Result<LeaseLifecycleResult>> SaveAsync(PropertyLease lease, CancellationToken cancellationToken, IReadOnlyList<string>? warnings = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<LeaseStatus>(lease.LeaseStatusId).LoadAsync(cancellationToken);
    return Result<LeaseLifecycleResult>.Success(new LeaseLifecycleResult(lease.Id.Value, refs[lease.LeaseStatusId], warnings ?? Array.Empty<string>()));
  }
}
