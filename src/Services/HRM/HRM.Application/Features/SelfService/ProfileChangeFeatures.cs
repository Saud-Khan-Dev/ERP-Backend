using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Employee Portal › My profile: the employee asks for a change to their own contacts, addresses, emergency contacts,
// family or photo; HR approves or rejects it. The change is checked against the record's rules when it is asked for
// (so the employee hears at once that a number is not a phone number), and made on approval through the very commands
// HR's own form uses - one primary contact per type, one permanent address, an active family relation, HR never
// changing their own record.

public sealed record ProfileChangeInput(
  ProfileChangeArea Area,
  ProfileChangeAction Action,
  Guid? TargetId,
  ContactInput? Contact,
  AddressInput? Address,
  EmergencyContactInput? EmergencyContact,
  FamilyMemberInput? FamilyMember,
  string? Note,
  Guid? SupportingDocumentId);

public sealed record ProfileChangeDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  ProfileChangeArea Area,
  ProfileChangeAction Action,
  Guid? TargetId,
  string Summary,
  string? Before,
  JsonElement? Proposed,
  bool HasPhoto,
  string? Note,
  Guid? SupportingDocumentId,
  string? SupportingDocument,
  ProfileChangeStatus Status,
  DateTime SubmittedAt,
  Guid? ReviewedBy,
  string? ReviewedByName,
  DateTime? ReviewedAt,
  string? ReviewRemarks);

public sealed record GetProfileChangesQueryResult(PaginatedResult<ProfileChangeDto> Changes);

public sealed record GetProfileChangesQuery(
  PaginationRequest Pagination,
  Guid? EmployeeId,
  ProfileChangeStatus? Status,
  string? Search,
  Guid? OnlyForEmployee = null) : IQuery<Result<GetProfileChangesQueryResult>>;

public sealed record GetProfileChangePhotoQuery(Guid Id, Guid? OnlyForEmployee = null) : IQuery<Result<StoredFile>>;

public sealed record SubmitProfileChangeCommand(Guid EmployeeId, ProfileChangeInput Change) : ICommand<Result<CreatedResult>>;
public sealed record SubmitPhotoChangeCommand(Guid EmployeeId, UploadedFile File, string? Note) : ICommand<Result<CreatedResult>>;
public sealed record WithdrawProfileChangeCommand(Guid Id, Guid EmployeeId) : ICommand<Result<UpdatedResult>>;
public sealed record DecideProfileChangeCommand(Guid Id, bool Approve, string? Remarks) : ICommand<Result<UpdatedResult>>;

public class SubmitProfileChangeCommandValidator : AbstractValidator<SubmitProfileChangeCommand>
{
  public SubmitProfileChangeCommandValidator()
  {
    RuleFor(x => x.Change).NotNull();
    RuleFor(x => x.Change.Area).NotEqual(ProfileChangeArea.Photo).WithMessage("Send a new photo as a file.");
    RuleFor(x => x.Change.Note).MaximumLength(1000);
    When(x => x.Change.Action != ProfileChangeAction.Remove, () =>
    {
      RuleFor(x => x.Change.Contact).NotNull().WithMessage("Give the contact.").SetValidator(new ContactInputValidator()!)
        .When(x => x.Change.Area == ProfileChangeArea.Contact);
      RuleFor(x => x.Change.Address).NotNull().WithMessage("Give the address.").SetValidator(new AddressInputValidator()!)
        .When(x => x.Change.Area == ProfileChangeArea.Address);
      RuleFor(x => x.Change.EmergencyContact).NotNull().WithMessage("Give the emergency contact.").SetValidator(new EmergencyContactInputValidator()!)
        .When(x => x.Change.Area == ProfileChangeArea.EmergencyContact);
      RuleFor(x => x.Change.FamilyMember).NotNull().WithMessage("Give the family member.").SetValidator(new FamilyMemberInputValidator()!)
        .When(x => x.Change.Area == ProfileChangeArea.FamilyMember);
    });
  }
}

public class SubmitPhotoChangeCommandValidator : AbstractValidator<SubmitPhotoChangeCommand>
{
  public SubmitPhotoChangeCommandValidator() => RuleFor(x => x.Note).MaximumLength(1000);
}

public class DecideProfileChangeCommandValidator : AbstractValidator<DecideProfileChangeCommand>
{
  public DecideProfileChangeCommandValidator()
  {
    RuleFor(x => x.Remarks).MaximumLength(1000);
    RuleFor(x => x.Remarks).NotEmpty().When(x => !x.Approve).WithMessage("Say why the change is not accepted, so the employee can put it right.");
  }
}

public class ProfileChangeHandlers(
    IApplicationDbContext context,
    ISender sender,
    FileUploads files,
    HrLookup lookup,
    ICurrentUser currentUser,
    IClock clock,
    DutyGuard duty,
    IAuditTrail audit) :
  IQueryHandler<GetProfileChangesQuery, Result<GetProfileChangesQueryResult>>,
  IQueryHandler<GetProfileChangePhotoQuery, Result<StoredFile>>,
  ICommandHandler<SubmitProfileChangeCommand, Result<CreatedResult>>,
  ICommandHandler<SubmitPhotoChangeCommand, Result<CreatedResult>>,
  ICommandHandler<WithdrawProfileChangeCommand, Result<UpdatedResult>>,
  ICommandHandler<DecideProfileChangeCommand, Result<UpdatedResult>>
{
  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

  public async Task<Result<GetProfileChangesQueryResult>> Handle(GetProfileChangesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.ProfileChanges.AsNoTracking();
    if ((query.OnlyForEmployee ?? query.EmployeeId) is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(r => r.EmployeeId == employeeId);
    }
    if (query.Status is { } status)
      rows = rows.Where(r => r.Status == status);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(r => EF.Functions.Like(r.Summary.ToLower(), pattern, SearchPattern.Escape)
        || context.Employees.Any(e => e.Id == r.EmployeeId && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape)
          || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    // waiting ones oldest first (the order HR works through them), decided ones newest first
    var ordered = query.Status == ProfileChangeStatus.Pending ? rows.OrderBy(r => r.SubmittedAt) : rows.OrderByDescending(r => r.SubmittedAt);
    var page = await ordered.Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await DtosAsync(page, cancellationToken);
    return Result<GetProfileChangesQueryResult>.Success(new(new PaginatedResult<ProfileChangeDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<StoredFile>> Handle(GetProfileChangePhotoQuery query, CancellationToken cancellationToken)
  {
    var change = await LoadAsync(query.Id, query.OnlyForEmployee, cancellationToken);
    if (change.PhotoPath is not { } path)
      throw new StoredFileNotFoundException("This change carries no photo.");
    return Result<StoredFile>.Success(await files.OpenAsync(path, "new-photo", cancellationToken));
  }

  public async Task<Result<CreatedResult>> Handle(SubmitProfileChangeCommand command, CancellationToken cancellationToken)
  {
    var input = command.Change;
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    if (input.TargetId is { } target && await WaitingOnAsync(employee.Id, input.Area, target, cancellationToken))
      return Result<CreatedResult>.Failure("A change to this entry is already waiting for HR. Withdraw it first, or wait for HR's decision.");
    if (input.Area == ProfileChangeArea.FamilyMember && input.FamilyMember is { } member)
      await context.EnsureRelationAsync(member.RelationId, cancellationToken);

    // the change is tried on a copy of the record: the record's own rules answer now, not when HR opens it
    var copy = await context.Employees.AsNoTracking()
      .Include(e => e.Contacts).Include(e => e.Addresses).Include(e => e.EmergencyContacts).Include(e => e.FamilyMembers)
      .FirstAsync(e => e.Id == employee.Id, cancellationToken);
    var before = await DescribeTargetAsync(copy, input, cancellationToken);
    TryOn(copy, input, clock.Today);

    var proposed = input.Action == ProfileChangeAction.Remove ? null : input.Area switch
    {
      ProfileChangeArea.Contact => JsonSerializer.Serialize(input.Contact, Json),
      ProfileChangeArea.Address => JsonSerializer.Serialize(input.Address, Json),
      ProfileChangeArea.EmergencyContact => JsonSerializer.Serialize(input.EmergencyContact, Json),
      ProfileChangeArea.FamilyMember => JsonSerializer.Serialize(input.FamilyMember, Json),
      _ => null
    };
    var what = input.Action == ProfileChangeAction.Remove ? before : await DescribeProposedAsync(input, cancellationToken);
    var summary = $"{EnumText.Label(input.Action)} {Noun(input.Area)}: {what}";
    var paper = input.SupportingDocumentId is { } paperId ? await context.LoadDocumentAsync(paperId, cancellationToken) : null;

    var change = ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), employee, input.Area, input.Action, input.TargetId, proposed, summary,
      input.Action == ProfileChangeAction.Add ? null : before, null, input.Note, paper, currentUser.UserId, clock.UtcNow);
    context.ProfileChanges.Add(change);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(change.Id);
  }

  public async Task<Result<CreatedResult>> Handle(SubmitPhotoChangeCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    if (await context.ProfileChanges.AnyAsync(r => r.EmployeeId == employee.Id && r.Area == ProfileChangeArea.Photo && r.Status == ProfileChangeStatus.Pending, cancellationToken))
      return Result<CreatedResult>.Failure("A new photo is already waiting for HR. Withdraw it first, or wait for HR's decision.");

    var path = await files.SavePhotoAsync(command.File, employee.EmployeeNumber, cancellationToken);
    var change = ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), employee, ProfileChangeArea.Photo, ProfileChangeAction.Change, null, null,
      "New photo", employee.PhotoReference is null ? null : "The photo on the record", path, command.Note, null, currentUser.UserId, clock.UtcNow);
    context.ProfileChanges.Add(change);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(change.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(WithdrawProfileChangeCommand command, CancellationToken cancellationToken)
  {
    var change = await LoadAsync(command.Id, command.EmployeeId, cancellationToken);
    change.Withdraw(clock.UtcNow);
    audit.Record(HrmVerbs.Cancelled, change, "Withdrawn by the employee");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DecideProfileChangeCommand command, CancellationToken cancellationToken)
  {
    var change = await LoadAsync(command.Id, null, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(change.EmployeeId, "decide a change to your own record", change, cancellationToken);
    var remarks = string.IsNullOrWhiteSpace(command.Remarks) ? null : $"Remarks: {command.Remarks.Trim()}";

    if (!command.Approve)
    {
      change.Reject(currentUser.UserId, clock.UtcNow, command.Remarks);
      audit.Record(HrmVerbs.Rejected, change, remarks);
      await context.SaveChangesAsync(cancellationToken);
      return CommandResults.Updated();
    }

    change.Approve(currentUser.UserId, clock.UtcNow, command.Remarks);
    audit.Record(HrmVerbs.Approved, change, remarks);

    if (change.Area == ProfileChangeArea.Photo)
    {
      var employee = await context.LoadEmployeeAsync(change.EmployeeId.Value, cancellationToken);
      employee.EnsureProfileActive();
      employee.SetPhoto(change.PhotoPath);
      await context.SaveChangesAsync(cancellationToken);
      return CommandResults.Updated();
    }

    // HR's own command makes the change and saves it together with the decision
    var failure = await ApplyAsync(change, cancellationToken);
    return failure is null ? CommandResults.Updated() : Result<UpdatedResult>.Failure(failure);
  }

  /// Makes an approved change on the record; the refusal in words when the record no longer allows it.
  private async Task<string?> ApplyAsync(ProfileChangeRequest change, CancellationToken cancellationToken)
  {
    var employee = change.EmployeeId.Value;
    var target = change.TargetId ?? Guid.Empty;
    var json = change.Proposed;
    T Read<T>() => JsonSerializer.Deserialize<T>(json!, Json)!;

    IBaseRequest request = (change.Area, change.Action) switch
    {
      (ProfileChangeArea.Contact, ProfileChangeAction.Add) => new AddContactCommand(employee, Read<ContactInput>()),
      (ProfileChangeArea.Contact, ProfileChangeAction.Change) => new UpdateContactCommand(employee, target, Read<ContactInput>()),
      (ProfileChangeArea.Contact, _) => new RemoveContactCommand(employee, target),
      (ProfileChangeArea.Address, ProfileChangeAction.Add) => new AddAddressCommand(employee, Read<AddressInput>()),
      (ProfileChangeArea.Address, ProfileChangeAction.Change) => new UpdateAddressCommand(employee, target, Read<AddressInput>()),
      (ProfileChangeArea.Address, _) => new RemoveAddressCommand(employee, target),
      (ProfileChangeArea.EmergencyContact, ProfileChangeAction.Add) => new AddEmergencyContactCommand(employee, Read<EmergencyContactInput>()),
      (ProfileChangeArea.EmergencyContact, ProfileChangeAction.Change) => new UpdateEmergencyContactCommand(employee, target, Read<EmergencyContactInput>()),
      (ProfileChangeArea.EmergencyContact, _) => new RemoveEmergencyContactCommand(employee, target),
      (ProfileChangeArea.FamilyMember, ProfileChangeAction.Add) => new AddFamilyMemberCommand(employee, Read<FamilyMemberInput>()),
      (ProfileChangeArea.FamilyMember, ProfileChangeAction.Change) => new UpdateFamilyMemberCommand(employee, target, Read<FamilyMemberInput>()),
      (ProfileChangeArea.FamilyMember, _) => new RemoveFamilyMemberCommand(employee, target),
      _ => throw new DomainException("This change cannot be made on the record.")
    };

    return await sender.Send(request, cancellationToken) switch
    {
      Result<CreatedResult> { IsSuccess: false } created => created.Message,
      Result<UpdatedResult> { IsSuccess: false } updated => updated.Message,
      _ => null
    };
  }

  /// Applies the change to a copy of the record that is never saved: the record's rules refuse it now, in the same words.
  private static void TryOn(Employee copy, ProfileChangeInput input, DateOnly today)
  {
    var target = input.TargetId ?? Guid.Empty;
    switch (input.Area, input.Action)
    {
      case (ProfileChangeArea.Contact, ProfileChangeAction.Add): copy.AddContact(input.Contact!.ContactType, input.Contact.Value, input.Contact.IsPrimary); break;
      case (ProfileChangeArea.Contact, ProfileChangeAction.Change): copy.UpdateContact(EmployeeContactId.Of(target), input.Contact!.ContactType, input.Contact.Value, input.Contact.IsPrimary); break;
      case (ProfileChangeArea.Contact, _): copy.RemoveContact(EmployeeContactId.Of(target)); break;
      case (ProfileChangeArea.Address, ProfileChangeAction.Add): copy.AddAddress(input.Address!.AddressType, input.Address.ToDetails()); break;
      case (ProfileChangeArea.Address, ProfileChangeAction.Change): copy.UpdateAddress(EmployeeAddressId.Of(target), input.Address!.AddressType, input.Address.ToDetails()); break;
      case (ProfileChangeArea.Address, _): copy.RemoveAddress(EmployeeAddressId.Of(target)); break;
      case (ProfileChangeArea.EmergencyContact, ProfileChangeAction.Add): copy.AddEmergencyContact(input.EmergencyContact!.Name, input.EmergencyContact.Relation, input.EmergencyContact.Phone); break;
      case (ProfileChangeArea.EmergencyContact, ProfileChangeAction.Change): copy.UpdateEmergencyContact(EmergencyContactId.Of(target), input.EmergencyContact!.Name, input.EmergencyContact.Relation, input.EmergencyContact.Phone); break;
      case (ProfileChangeArea.EmergencyContact, _): copy.RemoveEmergencyContact(EmergencyContactId.Of(target)); break;
      case (ProfileChangeArea.FamilyMember, ProfileChangeAction.Add): copy.AddFamilyMember(input.FamilyMember!.ToDetails(), today); break;
      case (ProfileChangeArea.FamilyMember, ProfileChangeAction.Change): copy.UpdateFamilyMember(FamilyMemberId.Of(target), input.FamilyMember!.ToDetails(), today); break;
      case (ProfileChangeArea.FamilyMember, _): copy.RemoveFamilyMember(FamilyMemberId.Of(target)); break;
    }
  }

  /// The entry as the record has it now (for a change or a removal).
  private async Task<string?> DescribeTargetAsync(Employee copy, ProfileChangeInput input, CancellationToken cancellationToken)
  {
    if (input.Action == ProfileChangeAction.Add || input.TargetId is not { } target)
      return null;
    var notYours = new DomainException($"That {Noun(input.Area)} is not on your record.");
    return input.Area switch
    {
      ProfileChangeArea.Contact => copy.Contacts.FirstOrDefault(c => c.Id.Value == target) is { } c ? Contact(c.ContactType, c.Value, c.IsPrimary) : throw notYours,
      ProfileChangeArea.Address => copy.Addresses.FirstOrDefault(a => a.Id.Value == target) is { } a
        ? Address(a.AddressType, new AddressDetails(a.AddressLine, a.City, a.District, a.Province, a.PostalCode)) : throw notYours,
      ProfileChangeArea.EmergencyContact => copy.EmergencyContacts.FirstOrDefault(e => e.Id.Value == target) is { } e ? Emergency(e.Name, e.Relation, e.Phone) : throw notYours,
      ProfileChangeArea.FamilyMember => copy.FamilyMembers.FirstOrDefault(m => m.Id.Value == target) is { } m
        ? await FamilyAsync(m.Name, m.RelationId?.Value, m.DateOfBirth, m.Cnic, m.Occupation, cancellationToken) : throw notYours,
      _ => null
    };
  }

  private async Task<string> DescribeProposedAsync(ProfileChangeInput input, CancellationToken cancellationToken) => input.Area switch
  {
    ProfileChangeArea.Contact => Contact(input.Contact!.ContactType, input.Contact.Value, input.Contact.IsPrimary),
    ProfileChangeArea.Address => Address(input.Address!.AddressType, input.Address.ToDetails()),
    ProfileChangeArea.EmergencyContact => Emergency(input.EmergencyContact!.Name, input.EmergencyContact.Relation, input.EmergencyContact.Phone),
    ProfileChangeArea.FamilyMember => await FamilyAsync(input.FamilyMember!.Name, input.FamilyMember.RelationId, input.FamilyMember.DateOfBirth,
      input.FamilyMember.Cnic, input.FamilyMember.Occupation, cancellationToken),
    _ => "New photo"
  };

  private static string Noun(ProfileChangeArea area) => area switch
  {
    ProfileChangeArea.Contact => "contact",
    ProfileChangeArea.Address => "address",
    ProfileChangeArea.EmergencyContact => "emergency contact",
    ProfileChangeArea.FamilyMember => "family member",
    _ => "photo"
  };

  private static string Contact(ContactType type, string value, bool primary) => $"{EnumText.Label(type)} {value.Trim()}{(primary ? " (main)" : "")}";

  private static string Address(AddressType type, AddressDetails a) =>
      $"{EnumText.Label(type)} address - {Join(a.AddressLine, a.City, a.District, a.Province, a.PostalCode) ?? "no details"}";

  private static string Emergency(string name, string? relation, string? phone) => Join(name, relation, phone) ?? name;

  private async Task<string> FamilyAsync(string? name, Guid? relationId, DateOnly? born, string? cnic, string? occupation, CancellationToken cancellationToken)
  {
    string? relation = null;
    if (relationId is { } id)
    {
      var key = FamilyRelationId.Of(id);
      relation = await context.FamilyRelations.AsNoTracking().Where(r => r.Id == key).Select(r => r.Name).FirstOrDefaultAsync(cancellationToken);
    }
    return Join(name ?? "(no name)", relation, born is { } d ? $"born {d.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}" : null,
      cnic is null ? null : $"CNIC {cnic}", occupation)!;
  }

  private static string? Join(params string?[] parts)
  {
    var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
    return kept.Count == 0 ? null : string.Join(", ", kept);
  }

  private Task<bool> WaitingOnAsync(EmployeeId employee, ProfileChangeArea area, Guid target, CancellationToken cancellationToken) =>
      context.ProfileChanges.AnyAsync(r => r.EmployeeId == employee && r.Area == area && r.TargetId == target && r.Status == ProfileChangeStatus.Pending, cancellationToken);

  private async Task<ProfileChangeRequest> LoadAsync(Guid id, Guid? onlyFor, CancellationToken cancellationToken)
  {
    var key = ProfileChangeRequestId.Of(id);
    var change = await context.ProfileChanges.FirstOrDefaultAsync(r => r.Id == key, cancellationToken);
    if (change is null || (onlyFor is { } own && change.EmployeeId.Value != own))
      throw new ProfileChangeNotFoundException($"Profile change {id} was not found.");
    return change;
  }

  private async Task<List<ProfileChangeDto>> DtosAsync(List<ProfileChangeRequest> changes, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(changes.Select(r => (EmployeeId?)r.EmployeeId), cancellationToken);
    var reviewerIds = changes.Where(r => r.ReviewedBy is not null).Select(r => r.ReviewedBy).Distinct().ToList();
    var reviewers = await context.Employees.AsNoTracking().Where(e => e.UserId != null && reviewerIds.Contains(e.UserId))
      .ToDictionaryAsync(e => e.UserId!.Value, e => e.FullName ?? e.EmployeeNumber, cancellationToken);
    var paperIds = changes.Where(r => r.SupportingDocumentId is not null).Select(r => r.SupportingDocumentId!).ToList();
    var papers = await (
        from d in context.Documents.AsNoTracking()
        where paperIds.Contains(d.Id)
        join t in context.DocumentTypes on d.DocumentTypeId equals t.Id
        select new { d.Id, t.Name, d.DocumentNumber })
      .ToDictionaryAsync(d => d.Id, d => d.DocumentNumber is null ? d.Name : $"{d.Name} {d.DocumentNumber}", cancellationToken);

    return changes.Select(r =>
    {
      var person = people.GetValueOrDefault(r.EmployeeId.Value);
      JsonElement? proposed = r.Proposed is { } json ? JsonDocument.Parse(json).RootElement.Clone() : null;
      return new ProfileChangeDto(r.Id.Value, r.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", r.Area, r.Action, r.TargetId,
        r.Summary, r.Before, proposed, r.PhotoPath is not null, r.Note, r.SupportingDocumentId?.Value,
        r.SupportingDocumentId is null ? null : papers.GetValueOrDefault(r.SupportingDocumentId), r.Status, r.SubmittedAt, r.ReviewedBy,
        r.ReviewedBy is { } by ? reviewers.GetValueOrDefault(by) : null, r.ReviewedAt, r.ReviewRemarks);
    }).ToList();
  }
}
