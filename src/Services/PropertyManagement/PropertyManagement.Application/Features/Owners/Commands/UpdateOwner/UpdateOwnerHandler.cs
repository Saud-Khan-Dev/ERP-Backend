using Microsoft.EntityFrameworkCore;

public class UpdateOwnerHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateOwnerCommand, Result<UpdateOwnerCommandResult>>
{
  public async Task<Result<UpdateOwnerCommandResult>> Handle(UpdateOwnerCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadVisibleOwnerAsync(command.Id, cancellationToken);
    var input = command.Owner;
    var cnic = Cnic.OfNullable(input.Cnic);
    var ntn = Ntn.OfNullable(input.Ntn);

    var duplicate = await OwnerDuplicates.FindAsync(context, cnic, ntn, owner.Id, cancellationToken);
    if (duplicate is not null)
      return Result<UpdateOwnerCommandResult>.Failure(duplicate);

    var holdsSince = await context.Ownerships.AsNoTracking()
      .Where(o => o.OwnerId == owner.Id && (o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Disputed))
      .MaxAsync(o => (DateOnly?)o.EffectiveFrom, cancellationToken);

    owner.Update(
      await masters.GetAsync<OwnerType>(input.OwnerTypeId, cancellationToken),
      Name.Of(input.OwnerName, PropertyOwner.NameMaxLength),
      input.FatherHusbandName, cnic, ntn,
      EmailAddress.OfNullable(input.Email), input.Remarks,
      input.OldIdentity, input.DateOfDeath, PropertyWriteGuard.Today(), holdsSince);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateOwnerCommandResult>.Success(new UpdateOwnerCommandResult(true));
  }
}
