public class CreateRentalHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateRentalCommand, Result<CreateRentalCommandResult>>
{
  public async Task<Result<CreateRentalCommandResult>> Handle(CreateRentalCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var tenant = await context.LoadOwnerAsync(command.TenantOwnerId, cancellationToken);

    var rental = PropertyRental.Create(
      RentalId.New(), property, tenant, await codes.NextAsync(CodeSequenceKeys.Rental, cancellationToken),
      await masters.GetByCodeAsync<RentalStatus>(command.AsDraft ? SystemMasterCodes.Draft : SystemMasterCodes.Active, cancellationToken),
      await command.Terms.ToTermsAsync(masters, cancellationToken));

    // one rental at a time on a property; a draft is checked when it is activated
    IReadOnlyList<string> warnings = Array.Empty<string>();
    if (!command.AsDraft)
    {
      context.Touch(property);
      warnings = AgreementOverlap.Check(rental.Span(tenant.OwnerName.Value), await context.RunningOnAsync(property.Id, cancellationToken));
    }

    await context.Rentals.AddAsync(rental, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateRentalCommandResult>.Success(new CreateRentalCommandResult(rental.Id.Value, rental.RentalNo.Value, warnings));
  }
}
