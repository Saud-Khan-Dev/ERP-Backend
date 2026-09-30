public class CreateRentalHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateRentalCommand, Result<CreateRentalCommandResult>>
{
  public async Task<Result<CreateRentalCommandResult>> Handle(CreateRentalCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var tenant = await context.LoadOwnerAsync(command.TenantOwnerId, cancellationToken);

    var rental = PropertyRental.Create(
      RentalId.New(), property, tenant, await codes.NextAsync(CodeSequenceKeys.Rental, cancellationToken),
      await masters.GetByCodeAsync<RentalStatus>(SystemMasterCodes.Active, cancellationToken),
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    await context.Rentals.AddAsync(rental, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateRentalCommandResult>.Success(new CreateRentalCommandResult(rental.Id.Value, rental.RentalNo.Value));
  }
}
