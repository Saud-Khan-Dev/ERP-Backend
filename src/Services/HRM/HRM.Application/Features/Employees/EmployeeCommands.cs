using FluentValidation;

public sealed record PersonalDetailsInput(
  string FirstName,
  string? MiddleName,
  string? LastName,
  string Cnic,
  DateOnly? DateOfBirth,
  Gender? Gender,
  string? Nationality,
  MaritalStatus? MaritalStatus,
  string? BloodGroup)
{
  public PersonalDetails ToDetails() => new(FirstName, MiddleName, LastName, Cnic, DateOfBirth, Gender, Nationality, MaritalStatus, BloodGroup);
}

public sealed record ContactInput(ContactType ContactType, string Value, bool IsPrimary);

public sealed record AddressInput(AddressType AddressType, string? AddressLine, string? City, string? District, string? Province, string? PostalCode)
{
  public AddressDetails ToDetails() => new(AddressLine, City, District, Province, PostalCode);
}

public sealed record EmergencyContactInput(string Name, string? Relation, string? Phone);

public sealed record FamilyMemberInput(string? Relation, string? Name, DateOnly? DateOfBirth, string? Cnic, string? Occupation)
{
  public FamilyMemberDetails ToDetails() => new(Relation, Name, DateOfBirth, Cnic, Occupation);
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
  EmploymentType EmploymentType,
  EmploymentMethod? EmploymentMethod,
  Guid? ProjectId,
  Guid? UserId,
  IReadOnlyList<ContactInput>? Contacts,
  IReadOnlyList<AddressInput>? Addresses,
  IReadOnlyList<EmergencyContactInput>? EmergencyContacts,
  IReadOnlyList<FamilyMemberInput>? FamilyMembers,
  AppointmentInput? Appointment) : ICommand<Result<RegisterEmployeeCommandResult>>;

public sealed record RegisterEmployeeCommandResult(Guid Id, string EmployeeNumber);

public sealed record UpdatePersonalDetailsCommand(Guid Id, PersonalDetailsInput Personal) : ICommand<Result<UpdatedResult>>;

/// Corrects the employment type / method / project (a regularization is recorded through an HR action instead).
public sealed record ChangeEmploymentCommand(Guid Id, EmploymentType EmploymentType, EmploymentMethod? EmploymentMethod, Guid? ProjectId) : ICommand<Result<UpdatedResult>>;

/// Corrects a typing mistake in the employee number.
public sealed record CorrectEmployeeNumberCommand(Guid Id, string EmployeeNumber) : ICommand<Result<UpdatedResult>>;

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
    RuleFor(x => x.Cnic).NotEmpty().MaximumLength(20);
    RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender.HasValue);
    RuleFor(x => x.MaritalStatus).IsInEnum().When(x => x.MaritalStatus.HasValue);
    RuleFor(x => x.Nationality).MaximumLength(60);
    RuleFor(x => x.BloodGroup).MaximumLength(5);
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
    RuleFor(x => x.Relation).MaximumLength(60);
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
    RuleFor(x => x.EmploymentType).IsInEnum();
    RuleFor(x => x.EmploymentMethod).IsInEnum().When(x => x.EmploymentMethod.HasValue);
    RuleForEach(x => x.Contacts).SetValidator(new ContactInputValidator());
    RuleForEach(x => x.Addresses).SetValidator(new AddressInputValidator());
    RuleForEach(x => x.EmergencyContacts).SetValidator(new EmergencyContactInputValidator());
    RuleForEach(x => x.FamilyMembers).SetValidator(new FamilyMemberInputValidator());
    RuleFor(x => x.Appointment!).SetValidator(new AppointmentInputValidator()).When(x => x.Appointment is not null);
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
