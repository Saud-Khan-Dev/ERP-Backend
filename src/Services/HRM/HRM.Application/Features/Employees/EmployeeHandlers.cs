using Microsoft.EntityFrameworkCore;

public class EmployeeHandlers(
  IApplicationDbContext context,
  CodeIssuer codes,
  ServiceRecordWriter writer,
  FileUploads files,
  ChangeLog log,
  ICurrentUser currentUser,
  IClock clock,
  IAuditTrail audit,
  OfficeScope scope,
  EmployeeScope employeeScope,
  Sensitive sensitive,
  DutyGuard duty) :
  ICommandHandler<RegisterEmployeeCommand, Result<RegisterEmployeeCommandResult>>,
  ICommandHandler<UpdatePersonalDetailsCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeEmploymentCommand, Result<UpdatedResult>>,
  ICommandHandler<CorrectEmployeeNumberCommand, Result<UpdatedResult>>,
  ICommandHandler<SetEmployeeTermCommand, Result<UpdatedResult>>,
  ICommandHandler<LinkEmployeeUserCommand, Result<UpdatedResult>>,
  ICommandHandler<SetEmployeeProfileStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<UploadEmployeePhotoCommand, Result<UpdatedResult>>
{
  public async Task<Result<RegisterEmployeeCommandResult>> Handle(RegisterEmployeeCommand command, CancellationToken cancellationToken)
  {
    var today = clock.Today;

    // a person limited to some offices registers people into them: the appointment decides where the employee counts
    if (scope.IsLimited)
    {
      if (command.Appointment is not { } placement)
        return Result<RegisterEmployeeCommandResult>.Failure(
          $"Your access is limited to {await scope.OfficeNamesAsync(cancellationToken)}: register the employee with an appointment to a post there.");
      var unit = await context.PostVersions.AsNoTracking()
        .Where(v => v.PostId == PostId.Of(placement.PostId) && v.EffectiveFrom <= placement.EffectiveFrom && (v.EffectiveTo == null || v.EffectiveTo >= placement.EffectiveFrom))
        .Select(v => v.OrgUnitId).FirstOrDefaultAsync(cancellationToken);
      if (unit is null || !await scope.CoversUnitAsync(unit, cancellationToken))
        return Result<RegisterEmployeeCommandResult>.Failure(
          $"The post is outside the offices you work on ({await scope.OfficeNamesAsync(cancellationToken)}). Choose a post there.");
    }

    // numbers and CNICs are unique across the whole organization, whatever the person may see
    using var everyone = employeeScope.Unrestricted();
    var number = string.IsNullOrWhiteSpace(command.EmployeeNumber)
      ? await codes.NextEmployeeNumberAsync(cancellationToken)
      : Employee.NormalizeNumber(command.EmployeeNumber);

    var employmentType = await context.LoadEmploymentTypeAsync(command.EmploymentTypeId, cancellationToken);
    var employmentMethod = await context.MethodAsync(command.EmploymentMethodId, cancellationToken);
    foreach (var member in command.FamilyMembers ?? [])
      await context.EnsureRelationAsync(member.RelationId, cancellationToken);
    var employee = Employee.Create(EmployeeId.New(), number, command.Personal.ToDetails(), employmentType, employmentMethod, command.ProjectId, today);

    if (await Clash(employee, command.UserId, cancellationToken) is { } clash)
      return Result<RegisterEmployeeCommandResult>.Failure(clash);

    if (command.UserId is { } userId)
      employee.LinkUser(userId);

    if (command.Term is { } term && (term.TermFrom is not null || term.TermTo is not null || !string.IsNullOrWhiteSpace(term.LendingDepartment)))
      employee.SetTerm(term.TermFrom, term.TermTo, term.LendingDepartment);

    foreach (var contact in command.Contacts ?? [])
      employee.AddContact(contact.ContactType, contact.Value, contact.IsPrimary);
    foreach (var address in command.Addresses ?? [])
      employee.AddAddress(address.AddressType, address.ToDetails());
    foreach (var contact in command.EmergencyContacts ?? [])
      employee.AddEmergencyContact(contact.Name, contact.Relation, contact.Phone);
    foreach (var member in command.FamilyMembers ?? [])
      employee.AddFamilyMember(member.ToDetails(), today);

    context.Employees.Add(employee);

    if (command.Appointment is { } appointment)
    {
      RecruitmentMethodId? methodId = null;
      if (appointment.RecruitmentMethodId is { } method)
      {
        methodId = RecruitmentMethodId.Of(method);
        var recruitment = await context.RecruitmentMethods.FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
          ?? throw new RecruitmentMethodNotFoundException($"Recruitment method {method} was not found.");
        recruitment.EnsureActive();
      }

      await writer.AppointAsync(employee, new AppointmentSpec(
        PostId.Of(appointment.PostId), appointment.EffectiveFrom,
        employee.EmploymentBasis == EmploymentBasis.Deputation ? ServiceEventNames.DeputationIn : ServiceEventNames.Appointment,
        appointment.OrderNumber, methodId, appointment.StageNumber, appointment.BasicPay, null,
        appointment.ExternalReferenceOrg ?? employee.LendingDepartment, null, appointment.Remarks, currentUser.UserId), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<RegisterEmployeeCommandResult>.Success(new(employee.Id.Value, employee.EmployeeNumber));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePersonalDetailsCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "change the personal details", employee, cancellationToken);

    var before = new PersonalDetails(employee.FirstName, employee.MiddleName, employee.LastName, employee.Cnic, employee.DateOfBirth, employee.Gender,
      employee.Nationality, employee.MaritalStatus, employee.BloodGroup);

    // someone who may not see the CNIC and date of birth sends them empty: they stay as they are
    var input = command.Personal;
    if (!sensitive.CanView)
      input = input with { Cnic = string.IsNullOrWhiteSpace(input.Cnic) ? employee.Cnic : input.Cnic, DateOfBirth = input.DateOfBirth ?? employee.DateOfBirth };
    if (string.IsNullOrWhiteSpace(input.Cnic))
      return Result<UpdatedResult>.Failure("Enter the CNIC.");

    var proposed = input.ToDetails();
    var identityChanges = Cnic.Normalize(proposed.Cnic) != employee.Cnic || proposed.DateOfBirth != employee.DateOfBirth;
    if (identityChanges && !sensitive.CanEdit)
      return Result<UpdatedResult>.Failure("Changing a CNIC or a date of birth needs the permission \"Change CNIC, date of birth and salary bank accounts\" (HR_SENSITIVE.EDIT).");

    employee.UpdatePersonalDetails(proposed, clock.Today);
    var after = new PersonalDetails(employee.FirstName, employee.MiddleName, employee.LastName, employee.Cnic, employee.DateOfBirth, employee.Gender,
      employee.Nationality, employee.MaritalStatus, employee.BloodGroup);

    using (employeeScope.Unrestricted())
    {
      if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.Cnic == employee.Cnic, cancellationToken))
        return Result<UpdatedResult>.Failure($"Another employee is registered with CNIC {employee.Cnic}.");
    }

    var changes = EmployeeChangeLogEntry.Diff(before, after);
    var reason = string.IsNullOrWhiteSpace(input.ChangeReason) ? null : input.ChangeReason.Trim();
    var needsReason = changes.Where(c => EmployeeFields.NeedReason.Contains(c.Field)).Select(c => c.Field).ToList();
    if (reason is null && needsReason.Count > 0)
      return Result<UpdatedResult>.Failure($"Say why the {(needsReason[0] == EmployeeFields.Cnic ? "CNIC" : "date of birth")} is corrected; the reason is kept in the employee's history.");

    foreach (var (name, oldValue, newValue) in changes)
      log.Add(employee.Id, name, oldValue, newValue, reason);

    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeEmploymentCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "correct the employment terms", employee, cancellationToken);
    var (oldTypeId, oldMethod) = (employee.EmploymentTypeId, employee.EmploymentMethodId);
    var oldProject = employee.ProjectId;
    var oldType = await context.LoadEmploymentTypeAsync(oldTypeId.Value, cancellationToken);
    var newType = await context.LoadEmploymentTypeAsync(command.EmploymentTypeId, cancellationToken);
    employee.CorrectEmployment(newType, await context.MethodAsync(command.EmploymentMethodId, cancellationToken, oldMethod), command.ProjectId);
    log.Add(employee.Id, EmployeeFields.EmploymentType, oldType.Name, newType.Name, command.Reason);
    var methods = await context.EmploymentMethods.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);
    log.Add(employee.Id, EmployeeFields.EmploymentMethod, oldMethod is null ? null : methods.GetValueOrDefault(oldMethod),
      employee.EmploymentMethodId is null ? null : methods.GetValueOrDefault(employee.EmploymentMethodId), command.Reason);
    log.Add(employee.Id, "Project", oldProject?.ToString(), employee.ProjectId?.ToString(), command.Reason);
    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectEmployeeNumberCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "correct the employee number", employee, cancellationToken);
    var number = Employee.NormalizeNumber(command.EmployeeNumber);

    using (employeeScope.Unrestricted())
    {
      if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.EmployeeNumber == number, cancellationToken))
        return Result<UpdatedResult>.Failure($"Employee number {number} is already in use.");
    }

    var old = employee.EmployeeNumber;
    employee.CorrectEmployeeNumber(number);
    log.Add(employee.Id, EmployeeFields.EmployeeNumber, old, employee.EmployeeNumber, command.Reason);
    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetEmployeeTermCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "set or extend the term", employee, cancellationToken);
    string? Term(DateOnly? from, DateOnly? to) => from is null && to is null ? null : $"{EmployeeChangeLogEntry.Day(from) ?? "…"} to {EmployeeChangeLogEntry.Day(to) ?? "open"}";
    var (oldTerm, oldDepartment) = (Term(employee.TermFrom, employee.TermTo), employee.LendingDepartment);
    employee.SetTerm(command.TermFrom, command.TermTo, command.LendingDepartment);
    log.Add(employee.Id, EmployeeFields.Term, oldTerm, Term(employee.TermFrom, employee.TermTo), command.Reason);
    log.Add(employee.Id, EmployeeFields.ParentDepartment, oldDepartment, employee.LendingDepartment, command.Reason);
    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(LinkEmployeeUserCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "link or unlink the portal account", employee, cancellationToken);
    var old = employee.UserId;

    if (command.UserId is { } userId)
    {
      // linking an account opens that person's portal (/me) to it: never your own account, never one linked elsewhere
      if (userId == currentUser.UserId)
        return Result<UpdatedResult>.Failure("You cannot link your own sign-in account to an employee record. Ask another administrator.");
      using (employeeScope.Unrestricted())
      {
        if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.UserId == userId, cancellationToken))
          return Result<UpdatedResult>.Failure("This sign-in account is already linked to another employee. Unlink it there first.");
      }
      employee.LinkUser(userId);
    }
    else
    {
      employee.UnlinkUser();
    }

    log.Add(employee.Id, EmployeeFields.SigninAccount, old?.ToString(), employee.UserId?.ToString(), command.UserId is null ? "Unlinked" : "Linked");
    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetEmployeeProfileStatusCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "switch the profile off or on", employee, cancellationToken);

    if (command.Status == RecordStatus.Inactive && !employee.HasLeftService)
    {
      var placed = await context.PositionAssignments.AnyAsync(a => a.EmployeeId == employee.Id && a.Status == RecordStatus.Active
        && (a.EffectiveTo == null || a.EffectiveTo >= clock.Today), cancellationToken);
      if (placed)
        return Result<UpdatedResult>.Failure($"{employee.DisplayName} still holds a post. Record the separation (or end the assignments) before deactivating the profile.");
    }

    var old = employee.ProfileStatus;
    employee.SetProfileStatus(command.Status);
    log.Add(employee.Id, EmployeeFields.Profile, EnumText.Label(old), EnumText.Label(employee.ProfileStatus));
    await SaveWithReasonsAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(UploadEmployeePhotoCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    employee.EnsureProfileActive();

    var path = await files.SavePhotoAsync(command.File, employee.EmployeeNumber, cancellationToken);
    employee.SetPhoto(path);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// Saves an identity or employment change: the change log writes one line per fact with its reason, so the automatic
  /// lines (which have no reason) are left out of this save.
  private async Task SaveWithReasonsAsync(CancellationToken cancellationToken)
  {
    using (audit.Quietly())
      await context.SaveChangesAsync(cancellationToken);
  }

  /// The number, CNIC and login each belong to one employee.
  private async Task<string?> Clash(Employee employee, Guid? userId, CancellationToken cancellationToken)
  {
    if (await context.Employees.AnyAsync(e => e.EmployeeNumber == employee.EmployeeNumber, cancellationToken))
      return $"Employee number {employee.EmployeeNumber} is already in use.";
    if (await context.Employees.AnyAsync(e => e.Cnic == employee.Cnic, cancellationToken))
      return $"An employee with CNIC {employee.Cnic} is already registered.";
    if (userId is { } user && await context.Employees.AnyAsync(e => e.UserId == user, cancellationToken))
      return "This user account is already linked to another employee.";
    return null;
  }
}
