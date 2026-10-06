using FluentValidation;

// Contacts, addresses, emergency contacts and family members of an employee: children of the Employee aggregate, so
// "one primary contact per type" and "one permanent / one current address" are checked in one place.

public sealed record AddContactCommand(Guid EmployeeId, ContactInput Contact) : ICommand<Result<CreatedResult>>;
public sealed record UpdateContactCommand(Guid EmployeeId, Guid ContactId, ContactInput Contact) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveContactCommand(Guid EmployeeId, Guid ContactId) : ICommand<Result<UpdatedResult>>;

public sealed record AddAddressCommand(Guid EmployeeId, AddressInput Address) : ICommand<Result<CreatedResult>>;
public sealed record UpdateAddressCommand(Guid EmployeeId, Guid AddressId, AddressInput Address) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveAddressCommand(Guid EmployeeId, Guid AddressId) : ICommand<Result<UpdatedResult>>;

public sealed record AddEmergencyContactCommand(Guid EmployeeId, EmergencyContactInput Contact) : ICommand<Result<CreatedResult>>;
public sealed record UpdateEmergencyContactCommand(Guid EmployeeId, Guid ContactId, EmergencyContactInput Contact) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveEmergencyContactCommand(Guid EmployeeId, Guid ContactId) : ICommand<Result<UpdatedResult>>;

public sealed record AddFamilyMemberCommand(Guid EmployeeId, FamilyMemberInput Member) : ICommand<Result<CreatedResult>>;
public sealed record UpdateFamilyMemberCommand(Guid EmployeeId, Guid MemberId, FamilyMemberInput Member) : ICommand<Result<UpdatedResult>>;
public sealed record RemoveFamilyMemberCommand(Guid EmployeeId, Guid MemberId) : ICommand<Result<UpdatedResult>>;

public class AddContactCommandValidator : AbstractValidator<AddContactCommand>
{
  public AddContactCommandValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new ContactInputValidator());
}

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
  public UpdateContactCommandValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new ContactInputValidator());
}

public class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
  public AddAddressCommandValidator() => RuleFor(x => x.Address).NotNull().SetValidator(new AddressInputValidator());
}

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
  public UpdateAddressCommandValidator() => RuleFor(x => x.Address).NotNull().SetValidator(new AddressInputValidator());
}

public class AddEmergencyContactCommandValidator : AbstractValidator<AddEmergencyContactCommand>
{
  public AddEmergencyContactCommandValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new EmergencyContactInputValidator());
}

public class UpdateEmergencyContactCommandValidator : AbstractValidator<UpdateEmergencyContactCommand>
{
  public UpdateEmergencyContactCommandValidator() => RuleFor(x => x.Contact).NotNull().SetValidator(new EmergencyContactInputValidator());
}

public class AddFamilyMemberCommandValidator : AbstractValidator<AddFamilyMemberCommand>
{
  public AddFamilyMemberCommandValidator() => RuleFor(x => x.Member).NotNull().SetValidator(new FamilyMemberInputValidator());
}

public class UpdateFamilyMemberCommandValidator : AbstractValidator<UpdateFamilyMemberCommand>
{
  public UpdateFamilyMemberCommandValidator() => RuleFor(x => x.Member).NotNull().SetValidator(new FamilyMemberInputValidator());
}

public class EmployeeRecordHandlers(IApplicationDbContext context, IClock clock) :
  ICommandHandler<AddContactCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateContactCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveContactCommand, Result<UpdatedResult>>,
  ICommandHandler<AddAddressCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateAddressCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveAddressCommand, Result<UpdatedResult>>,
  ICommandHandler<AddEmergencyContactCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateEmergencyContactCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveEmergencyContactCommand, Result<UpdatedResult>>,
  ICommandHandler<AddFamilyMemberCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateFamilyMemberCommand, Result<UpdatedResult>>,
  ICommandHandler<RemoveFamilyMemberCommand, Result<UpdatedResult>>
{
  public Task<Result<CreatedResult>> Handle(AddContactCommand command, CancellationToken cancellationToken) =>
      Add(command.EmployeeId, e => e.AddContact(command.Contact.ContactType, command.Contact.Value, command.Contact.IsPrimary).Id, cancellationToken);

  public Task<Result<UpdatedResult>> Handle(UpdateContactCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.UpdateContact(EmployeeContactId.Of(command.ContactId), command.Contact.ContactType, command.Contact.Value, command.Contact.IsPrimary), cancellationToken);

  public Task<Result<UpdatedResult>> Handle(RemoveContactCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.RemoveContact(EmployeeContactId.Of(command.ContactId)), cancellationToken);

  public Task<Result<CreatedResult>> Handle(AddAddressCommand command, CancellationToken cancellationToken) =>
      Add(command.EmployeeId, e => e.AddAddress(command.Address.AddressType, command.Address.ToDetails()).Id, cancellationToken);

  public Task<Result<UpdatedResult>> Handle(UpdateAddressCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.UpdateAddress(EmployeeAddressId.Of(command.AddressId), command.Address.AddressType, command.Address.ToDetails()), cancellationToken);

  public Task<Result<UpdatedResult>> Handle(RemoveAddressCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.RemoveAddress(EmployeeAddressId.Of(command.AddressId)), cancellationToken);

  public Task<Result<CreatedResult>> Handle(AddEmergencyContactCommand command, CancellationToken cancellationToken) =>
      Add(command.EmployeeId, e => e.AddEmergencyContact(command.Contact.Name, command.Contact.Relation, command.Contact.Phone).Id, cancellationToken);

  public Task<Result<UpdatedResult>> Handle(UpdateEmergencyContactCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.UpdateEmergencyContact(EmergencyContactId.Of(command.ContactId), command.Contact.Name, command.Contact.Relation, command.Contact.Phone), cancellationToken);

  public Task<Result<UpdatedResult>> Handle(RemoveEmergencyContactCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.RemoveEmergencyContact(EmergencyContactId.Of(command.ContactId)), cancellationToken);

  public Task<Result<CreatedResult>> Handle(AddFamilyMemberCommand command, CancellationToken cancellationToken) =>
      Add(command.EmployeeId, e => e.AddFamilyMember(command.Member.ToDetails(), clock.Today).Id, cancellationToken);

  public Task<Result<UpdatedResult>> Handle(UpdateFamilyMemberCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.UpdateFamilyMember(FamilyMemberId.Of(command.MemberId), command.Member.ToDetails(), clock.Today), cancellationToken);

  public Task<Result<UpdatedResult>> Handle(RemoveFamilyMemberCommand command, CancellationToken cancellationToken) =>
      Change(command.EmployeeId, e => e.RemoveFamilyMember(FamilyMemberId.Of(command.MemberId)), cancellationToken);

  private async Task<Result<CreatedResult>> Add(Guid employeeId, Func<Employee, ITypedId> add, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(employeeId, cancellationToken, withDetails: true);
    var id = add(employee);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(id);
  }

  private async Task<Result<UpdatedResult>> Change(Guid employeeId, Action<Employee> change, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(employeeId, cancellationToken, withDetails: true);
    change(employee);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
