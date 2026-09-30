public class CreateOwnerHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<CreateOwnerCommand, Result<CreateOwnerCommandResult>>
{
  public async Task<Result<CreateOwnerCommandResult>> Handle(CreateOwnerCommand command, CancellationToken cancellationToken)
  {
    var input = command.Owner;
    var cnic = Cnic.OfNullable(input.Cnic);

    var duplicate = await OwnerDuplicates.FindAsync(context, cnic, input.Ntn, null, cancellationToken);
    if (duplicate is not null)
      return Result<CreateOwnerCommandResult>.Failure(duplicate);

    var owner = PropertyOwner.Create(
      OwnerId.New(),
      await codes.NextAsync(CodeSequenceKeys.Owner, cancellationToken),
      await masters.GetAsync<OwnerType>(input.OwnerTypeId, cancellationToken),
      Name.Of(input.OwnerName, PropertyOwner.NameMaxLength),
      input.FatherHusbandName, cnic, input.Ntn, input.RegistrationNo,
      EmailAddress.OfNullable(input.Email), input.Remarks);

    foreach (var contact in command.Contacts ?? Array.Empty<ContactInput>())
      owner.AddContact(await masters.GetAsync<ContactType>(contact.ContactTypeId, cancellationToken), contact.ContactNumber, contact.IsPrimary, contact.Remarks);

    foreach (var address in command.Addresses ?? Array.Empty<AddressInput>())
      owner.AddAddress(address.AddressType, address.FullAddress, address.CityTown, address.District, address.Province, address.Country, address.PostalCode, address.IsPrimary);

    await context.Owners.AddAsync(owner, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateOwnerCommandResult>.Success(new CreateOwnerCommandResult(owner.Id.Value, owner.OwnerCode.Value));
  }
}
