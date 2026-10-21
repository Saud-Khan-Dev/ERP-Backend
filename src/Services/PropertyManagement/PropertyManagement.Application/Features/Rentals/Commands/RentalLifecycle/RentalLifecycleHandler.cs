public class RentalLifecycleHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<ActivateRentalCommand, Result<RentalLifecycleResult>>,
    ICommandHandler<ExpireRentalCommand, Result<RentalLifecycleResult>>,
    ICommandHandler<TerminateRentalCommand, Result<RentalLifecycleResult>>,
    ICommandHandler<WithdrawRentalDraftCommand, Result<RentalLifecycleResult>>,
    ICommandHandler<RenewRentalCommand, Result<RenewRentalCommandResult>>
{
  public async Task<Result<RentalLifecycleResult>> Handle(ActivateRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(rental.PropertyId, cancellationToken);
    rental.Activate(await Current(rental, cancellationToken), await Status(SystemMasterCodes.Active, cancellationToken));

    context.Touch(property);
    var tenant = await context.LoadOwnerAsync(rental.TenantOwnerId.Value, cancellationToken);
    var warnings = AgreementOverlap.Check(rental.Span(tenant.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken));
    return await SaveAsync(rental, cancellationToken, warnings);
  }

  public async Task<Result<RentalLifecycleResult>> Handle(ExpireRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(rental.PropertyId, cancellationToken);
    rental.Expire(await Current(rental, cancellationToken), await Status(SystemMasterCodes.Expired, cancellationToken), PropertyWriteGuard.Today());
    return await SaveAsync(rental, cancellationToken);
  }

  public async Task<Result<RentalLifecycleResult>> Handle(TerminateRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(rental.PropertyId, cancellationToken);
    rental.Terminate(await Current(rental, cancellationToken), await Status(SystemMasterCodes.Terminated, cancellationToken),
      command.TerminationDate, command.Reason, PropertyWriteGuard.Today());
    return await SaveAsync(rental, cancellationToken);
  }

  public async Task<Result<RentalLifecycleResult>> Handle(WithdrawRentalDraftCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(rental.PropertyId, cancellationToken);
    rental.WithdrawDraft(await Current(rental, cancellationToken), await Status(SystemMasterCodes.Withdrawn, cancellationToken), command.Reason);
    return await SaveAsync(rental, cancellationToken);
  }

  public async Task<Result<RenewRentalCommandResult>> Handle(RenewRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    var property = await context.EnsurePropertyActiveAsync(rental.PropertyId, cancellationToken);
    var tenant = await context.LoadOwnerAsync(command.TenantOwnerId ?? rental.TenantOwnerId.Value, cancellationToken);

    var renewal = rental.Renew(
      await Current(rental, cancellationToken),
      await Status(SystemMasterCodes.Ended, cancellationToken),
      await Status(SystemMasterCodes.Active, cancellationToken),
      RentalId.New(),
      await codes.NextAsync(CodeSequenceKeys.Rental, cancellationToken),
      tenant,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    // the renewal replaces this rental: it may not overlap any other rental
    context.Touch(property);
    var warnings = AgreementOverlap.Check(renewal.Span(tenant.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken), except: rental.RentalNo.Value);

    await context.Rentals.AddAsync(renewal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RenewRentalCommandResult>.Success(new RenewRentalCommandResult(renewal.Id.Value, renewal.RentalNo.Value, rental.Id.Value, warnings));
  }

  private Task<RentalStatus> Current(PropertyRental rental, CancellationToken cancellationToken) =>
      masters.GetAsync<RentalStatus>(rental.RentalStatusId.Value, cancellationToken);

  private Task<RentalStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<RentalStatus>(code, cancellationToken);

  private async Task<Result<RentalLifecycleResult>> SaveAsync(PropertyRental rental, CancellationToken cancellationToken, IReadOnlyList<string>? warnings = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<RentalStatus>(rental.RentalStatusId).LoadAsync(cancellationToken);
    return Result<RentalLifecycleResult>.Success(new RentalLifecycleResult(rental.Id.Value, refs[rental.RentalStatusId], warnings ?? Array.Empty<string>()));
  }
}
