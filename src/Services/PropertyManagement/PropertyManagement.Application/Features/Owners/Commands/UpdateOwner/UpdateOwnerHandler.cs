public class UpdateOwnerHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateOwnerCommand, Result<UpdateOwnerCommandResult>>
{
  public async Task<Result<UpdateOwnerCommandResult>> Handle(UpdateOwnerCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.Id, cancellationToken);
    var input = command.Owner;
    var cnic = Cnic.OfNullable(input.Cnic);

    var duplicate = await OwnerDuplicates.FindAsync(context, cnic, input.Ntn, owner.Id, cancellationToken);
    if (duplicate is not null)
      return Result<UpdateOwnerCommandResult>.Failure(duplicate);

    owner.Update(
      await masters.GetAsync<OwnerType>(input.OwnerTypeId, cancellationToken),
      Name.Of(input.OwnerName, PropertyOwner.NameMaxLength),
      input.FatherHusbandName, cnic, input.Ntn, input.RegistrationNo,
      EmailAddress.OfNullable(input.Email), input.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateOwnerCommandResult>.Success(new UpdateOwnerCommandResult(true));
  }
}
