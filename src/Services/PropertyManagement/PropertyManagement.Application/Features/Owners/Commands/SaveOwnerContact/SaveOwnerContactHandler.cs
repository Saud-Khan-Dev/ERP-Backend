public class SaveOwnerContactHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<SaveOwnerContactCommand, Result<SaveOwnerContactCommandResult>>,
    ICommandHandler<DeactivateOwnerContactCommand, Result<SaveOwnerContactCommandResult>>
{
  public async Task<Result<SaveOwnerContactCommandResult>> Handle(SaveOwnerContactCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.OwnerId, cancellationToken);
    var input = command.Contact;
    var type = await masters.GetAsync<ContactType>(input.ContactTypeId, cancellationToken);

    OwnerContactId id;
    if (command.ContactId is { } contactId)
    {
      id = OwnerContactId.Of(contactId);
      owner.UpdateContact(id, type, input.ContactNumber, input.IsPrimary, input.Remarks);
    }
    else
    {
      id = owner.AddContact(type, input.ContactNumber, input.IsPrimary, input.Remarks).Id;
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SaveOwnerContactCommandResult>.Success(new SaveOwnerContactCommandResult(id.Value));
  }

  public async Task<Result<SaveOwnerContactCommandResult>> Handle(DeactivateOwnerContactCommand command, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(command.OwnerId, cancellationToken);
    owner.DeactivateContact(OwnerContactId.Of(command.ContactId));

    await context.SaveChangesAsync(cancellationToken);
    return Result<SaveOwnerContactCommandResult>.Success(new SaveOwnerContactCommandResult(command.ContactId));
  }
}
