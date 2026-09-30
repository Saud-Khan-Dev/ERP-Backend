public class RentalLifecycleHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<TerminateRentalCommand, Result<RentalLifecycleResult>>,
    ICommandHandler<RenewRentalCommand, Result<RenewRentalCommandResult>>
{
  public async Task<Result<RentalLifecycleResult>> Handle(TerminateRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    rental.Terminate(await Current(rental, cancellationToken), await Status(SystemMasterCodes.Ended, cancellationToken),
      command.TerminationDate, command.Reason);

    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<RentalStatus>(rental.RentalStatusId).LoadAsync(cancellationToken);
    return Result<RentalLifecycleResult>.Success(new RentalLifecycleResult(rental.Id.Value, refs[rental.RentalStatusId]));
  }

  public async Task<Result<RenewRentalCommandResult>> Handle(RenewRentalCommand command, CancellationToken cancellationToken)
  {
    var rental = await context.LoadRentalAsync(command.Id, cancellationToken);
    var tenant = await context.LoadOwnerAsync(command.TenantOwnerId ?? rental.TenantOwnerId.Value, cancellationToken);

    var renewal = rental.Renew(
      await Current(rental, cancellationToken),
      await Status(SystemMasterCodes.Ended, cancellationToken),
      await Status(SystemMasterCodes.Active, cancellationToken),
      RentalId.New(),
      await codes.NextAsync(CodeSequenceKeys.Rental, cancellationToken),
      tenant,
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.Rentals.AddAsync(renewal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RenewRentalCommandResult>.Success(new RenewRentalCommandResult(renewal.Id.Value, renewal.RentalNo.Value, rental.Id.Value));
  }

  private Task<RentalStatus> Current(PropertyRental rental, CancellationToken cancellationToken) =>
      masters.GetAsync<RentalStatus>(rental.RentalStatusId.Value, cancellationToken);

  private Task<RentalStatus> Status(string code, CancellationToken cancellationToken) =>
      masters.GetByCodeAsync<RentalStatus>(code, cancellationToken);
}
