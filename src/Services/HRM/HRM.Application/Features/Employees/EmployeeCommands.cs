using FluentValidation;

public sealed record PersonalDetailsInput(
  string FirstName,
  string? MiddleName,
  string? LastName,
  /// Required at registration. On an update, left empty by someone who may not see it (HR_SENSITIVE.VIEW) it stays as it is.
  string? Cnic,
  DateOnly? DateOfBirth,
  Gender? Gender,
  string? Nationality,
  MaritalStatus? MaritalStatus,
  string? BloodGroup,
  /// Why the CNIC or the date of birth is corrected (required when either changes; kept in the change log).
  string? ChangeReason = null)
{
  public PersonalDetails ToDetails() => new(FirstName, MiddleName, LastName, Cnic ?? "", DateOfBirth, Gender, Nationality, MaritalStatus, BloodGroup);
}

/// The engagement period of a contract, deputation-in, ad hoc, daily-wage or project employee.
public sealed record TermInput(DateOnly? TermFrom, DateOnly? TermTo, string? LendingDepartment);

public sealed record ContactInput(ContactType ContactType, string Value, bool IsPrimary);

public sealed record AddressInput(AddressType AddressType, string? AddressLine, string? City, string? District, string? Province, string? PostalCode)
{
  public AddressDetails ToDetails() => new(AddressLine, City, District, Province, PostalCode);
}

public sealed record EmergencyContactInput(string Name, string? Relation, string? Phone);

public sealed record FamilyMemberInput(Guid? RelationId, string? Name, DateOnly? DateOfBirth, string? Cnic, string? Occupation)
{
  public FamilyMemberDetails ToDetails() => new(RelationId is { } relation ? FamilyRelationId.Of(relation) : null, Name, DateOfBirth, Cnic, Occupation);
}

/// The first post: the appointment (or deputation-in) date, the post, and the pay to start on (stage 0 unless given).
public sealed record AppointmentInput(
  Guid PostId,
  DateOnly EffectiveFrom,
  string? OrderNumber,
  Guid? RecruitmentMethodId,
  int? StageNumber,
  decimal? BasicPay,
  string? ExternalReferenceOrg,
  string? Remarks);

/// Registers an employee in ONE transaction: the master record, contacts, addresses, emergency contacts, family and -
/// when given - the appointment (service-history row, regular assignment to the post and the starting pay). Leave
/// EmployeeNumber empty to have the next EMP-### number issued.
public sealed record RegisterEmployeeCommand(
  string? EmployeeNumber,
  PersonalDetailsInput Personal,
  Guid EmploymentTypeId,
  Guid? EmploymentMethodId,
  Guid? ProjectId,
  Guid? UserId,
  IReadOnlyList<ContactInput>? Contacts,
  IReadOnlyList<AddressInput>? Addresses,
  IReadOnlyList<EmergencyContactInput>? EmergencyContacts,
  IReadOnlyList<FamilyMemberInput>? FamilyMembers,
  AppointmentInput? Appointment,
  TermInput? Term = null) : ICommand<Result<RegisterEmployeeCommandResult>>;

public sealed record RegisterEmployeeCommandResult(Guid Id, string EmployeeNumber);

public sealed record UpdatePersonalDetailsCommand(Guid Id, PersonalDetailsInput Personal) : ICommand<Result<UpdatedResult>>;

/// Corrects the employment type / method / project entered by mistake, with the reason. Making a contract (or other)
/// employee regular is a regularization: an HR action, approved and recorded in the service book.
public sealed record ChangeEmploymentCommand(Guid Id, Guid EmploymentTypeId, Guid? EmploymentMethodId, Guid? ProjectId, string? Reason = null) : ICommand<Result<UpdatedResult>>;

/// Corrects a typing mistake in the employee number.
public sealed record CorrectEmployeeNumberCommand(Guid Id, string EmployeeNumber, string? Reason = null) : ICommand<Result<UpdatedResult>>;

/// Sets or extends the engagement period of a non-regular employee (a contract extension is a new end date).
public sealed record SetEmployeeTermCommand(Guid Id, DateOnly? TermFrom, DateOnly? TermTo, string? LendingDepartment, string? Reason) : ICommand<Result<UpdatedResult>>;

/// Links (UserId set) or unlinks (null) the employee's Identity login.
public sealed record LinkEmployeeUserCommand(Guid Id, Guid? UserId) : ICommand<Result<UpdatedResult>>;

public sealed record SetEmployeeProfileStatusCommand(Guid Id, RecordStatus Status) : ICommand<Result<UpdatedResult>>;

public sealed record UploadEmployeePhotoCommand(Guid Id, UploadedFile File) : ICommand<Result<UpdatedResult>>;

public class PersonalDetailsInputValidator : AbstractValidator<PersonalDetailsInput>
{
  public PersonalDetailsInputValidator()
  {
    RuleFor(x => x.FirstName).NotEmpty().MaximumLength(Employee.NameMaxLength);
    RuleFor(x => x.MiddleName).MaximumLength(Employee.NameMaxLength);
    RuleFor(x => x.LastName).MaximumLength(Employee.NameMaxLength);
    RuleFor(x => x.Cnic).MaximumLength(20);
    RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender.HasValue);
    RuleFor(x => x.MaritalStatus).IsInEnum().When(x => x.MaritalStatus.HasValue);
    RuleFor(x => x.Nationality).MaximumLength(60);
    RuleFor(x => x.BloodGroup).MaximumLength(5);
    RuleFor(x => x.ChangeReason).MaximumLength(1000);
  }
}

public class TermInputValidator : AbstractValidator<TermInput>
{
  public TermInputValidator()
  {
    RuleFor(x => x.LendingDepartment).MaximumLength(200);
    RuleFor(x => x.TermTo).GreaterThanOrEqualTo(x => x.TermFrom).When(x => x.TermFrom is not null && x.TermTo is not null)
      .WithMessage("The term cannot end before it starts.");
  }
}

public class ChangeEmploymentCommandValidator : AbstractValidator<ChangeEmploymentCommand>
{
  public ChangeEmploymentCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.EmploymentTypeId).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the employment is corrected.").MaximumLength(1000);
  }
}

public class SetEmployeeTermCommandValidator : AbstractValidator<SetEmployeeTermCommand>
{
  public SetEmployeeTermCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.LendingDepartment).MaximumLength(200);
    RuleFor(x => x.Reason).MaximumLength(1000);
    RuleFor(x => x.TermTo).GreaterThanOrEqualTo(x => x.TermFrom).When(x => x.TermFrom is not null && x.TermTo is not null)
      .WithMessage("The term cannot end before it starts.");
  }
}

public class ContactInputValidator : AbstractValidator<ContactInput>
{
  public ContactInputValidator()
  {
    RuleFor(x => x.ContactType).IsInEnum();
    RuleFor(x => x.Value).NotEmpty().MaximumLength(150);
  }
}

public class AddressInputValidator : AbstractValidator<AddressInput>
{
  public AddressInputValidator()
  {
    RuleFor(x => x.AddressType).IsInEnum();
    RuleFor(x => x.AddressLine).MaximumLength(300);
    RuleFor(x => x.City).MaximumLength(100);
    RuleFor(x => x.District).MaximumLength(100);
    RuleFor(x => x.Province).MaximumLength(100);
    RuleFor(x => x.PostalCode).MaximumLength(20);
  }
}

public class EmergencyContactInputValidator : AbstractValidator<EmergencyContactInput>
{
  public EmergencyContactInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.Relation).MaximumLength(60);
    RuleFor(x => x.Phone).MaximumLength(30);
  }
}

public class FamilyMemberInputValidator : AbstractValidator<FamilyMemberInput>
{
  public FamilyMemberInputValidator()
  {
    RuleFor(x => x.Name).MaximumLength(150);
    RuleFor(x => x.Cnic).MaximumLength(20);
    RuleFor(x => x.Occupation).MaximumLength(100);
  }
}

public class AppointmentInputValidator : AbstractValidator<AppointmentInput>
{
  public AppointmentInputValidator()
  {
    RuleFor(x => x.PostId).NotEmpty();
    RuleFor(x => x.OrderNumber).MaximumLength(100);
    RuleFor(x => x.StageNumber).GreaterThanOrEqualTo(0).When(x => x.StageNumber.HasValue);
    RuleFor(x => x.BasicPay).GreaterThanOrEqualTo(0).When(x => x.BasicPay.HasValue);
    RuleFor(x => x.ExternalReferenceOrg).MaximumLength(200);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class RegisterEmployeeCommandValidator : AbstractValidator<RegisterEmployeeCommand>
{
  public RegisterEmployeeCommandValidator()
  {
    RuleFor(x => x.EmployeeNumber).MaximumLength(Employee.NumberMaxLength);
    RuleFor(x => x.Personal).NotNull().SetValidator(new PersonalDetailsInputValidator());
    RuleFor(x => x.EmploymentTypeId).NotEmpty();
    RuleForEach(x => x.Contacts).SetValidator(new ContactInputValidator());
    RuleForEach(x => x.Addresses).SetValidator(new AddressInputValidator());
    RuleForEach(x => x.EmergencyContacts).SetValidator(new EmergencyContactInputValidator());
    RuleForEach(x => x.FamilyMembers).SetValidator(new FamilyMemberInputValidator());
    RuleFor(x => x.Appointment!).SetValidator(new AppointmentInputValidator()).When(x => x.Appointment is not null);
    RuleFor(x => x.Term!).SetValidator(new TermInputValidator()).When(x => x.Term is not null);
  }
}

public class UpdatePersonalDetailsCommandValidator : AbstractValidator<UpdatePersonalDetailsCommand>
{
  public UpdatePersonalDetailsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Personal).NotNull().SetValidator(new PersonalDetailsInputValidator());
  }
}

public class CorrectEmployeeNumberCommandValidator : AbstractValidator<CorrectEmployeeNumberCommand>
{
  public CorrectEmployeeNumberCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(Employee.NumberMaxLength);
    RuleFor(x => x.Reason).MaximumLength(1000);
  }
}

public class UploadEmployeePhotoCommandValidator : AbstractValidator<UploadEmployeePhotoCommand>
{
  public UploadEmployeePhotoCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.File.FileName).NotEmpty().MaximumLength(255);
  }
}
