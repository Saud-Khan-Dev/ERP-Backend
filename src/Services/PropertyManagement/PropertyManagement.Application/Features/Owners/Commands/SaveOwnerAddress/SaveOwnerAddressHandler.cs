public class SaveOwnerAddressHandler(IApplicationDbContext context)
  : ICommandHandler<SaveOwnerAddressCommand, Result<SaveOwnerAddressCommandResult>>,
    ICommandHandler<DeactivateOwnerAddressCommand, Result<SaveOwnerAddressCommandResult>>
{
  public async Task<Result<SaveOwnerAddressCommandResult>> Handle(SaveOwnerAddressCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.OwnerId, cancellationToken);
    var a = command.Address;

    OwnerAddressId id;
    if (command.AddressId is { } addressId)
    {
      id = OwnerAddressId.Of(addressId);
      owner.UpdateAddress(id, a.AddressType, a.FullAddress, a.CityTown, a.District, a.Province, a.Country, a.PostalCode, a.IsPrimary);
    }
    else
    {
      id = owner.AddAddress(a.AddressType, a.FullAddress, a.CityTown, a.District, a.Province, a.Country, a.PostalCode, a.IsPrimary).Id;
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SaveOwnerAddressCommandResult>.Success(new SaveOwnerAddressCommandResult(id.Value));
  }

  public async Task<Result<SaveOwnerAddressCommandResult>> Handle(DeactivateOwnerAddressCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.OwnerId, cancellationToken);
    owner.DeactivateAddress(OwnerAddressId.Of(command.AddressId));

    await context.SaveChangesAsync(cancellationToken);
    return Result<SaveOwnerAddressCommandResult>.Success(new SaveOwnerAddressCommandResult(command.AddressId));
  }
}
