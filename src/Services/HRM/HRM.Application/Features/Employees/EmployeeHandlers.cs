using Microsoft.EntityFrameworkCore;

public class EmployeeHandlers(
  IApplicationDbContext context,
  CodeIssuer codes,
  ServiceRecordWriter writer,
  FileUploads files,
  ChangeLog log,
  ICurrentUser currentUser,
  IClock clock) :
  ICommandHandler<RegisterEmployeeCommand, Result<RegisterEmployeeCommandResult>>,
  ICommandHandler<UpdatePersonalDetailsCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeEmploymentCommand, Result<UpdatedResult>>,
  ICommandHandler<CorrectEmployeeNumberCommand, Result<UpdatedResult>>,
  ICommandHandler<SetEmployeeTermCommand, Result<UpdatedResult>>,
  ICommandHandler<LinkEmployeeUserCommand, Result<UpdatedResult>>,
  ICommandHandler<SetEmployeeProfileStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<UploadEmployeePhotoCommand, Result<UpdatedResult>>,
  IQueryHandler<GetEmployeeChangeLogQuery, Result<GetEmployeeChangeLogQueryResult>>
{
  public async Task<Result<RegisterEmployeeCommandResult>> Handle(RegisterEmployeeCommand command, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var number = string.IsNullOrWhiteSpace(command.EmployeeNumber)
      ? await codes.NextEmployeeNumberAsync(cancellationToken)
      : Employee.NormalizeNumber(command.EmployeeNumber);

    var employee = Employee.Create(EmployeeId.New(), number, command.Personal.ToDetails(), command.EmploymentType, command.EmploymentMethod, command.ProjectId, today);

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
        employee.EmploymentType == EmploymentType.DeputationIn ? ServiceEventNames.DeputationIn : ServiceEventNames.Appointment,
        appointment.OrderNumber, methodId, appointment.StageNumber, appointment.BasicPay, null,
        appointment.ExternalReferenceOrg ?? employee.LendingDepartment, null, appointment.Remarks, currentUser.UserId), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<RegisterEmployeeCommandResult>.Success(new(employee.Id.Value, employee.EmployeeNumber));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePersonalDetailsCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    var before = new PersonalDetails(employee.FirstName, employee.MiddleName, employee.LastName, employee.Cnic, employee.DateOfBirth, employee.Gender,
      employee.Nationality, employee.MaritalStatus, employee.BloodGroup);
    employee.UpdatePersonalDetails(command.Personal.ToDetails(), clock.Today);
    var after = new PersonalDetails(employee.FirstName, employee.MiddleName, employee.LastName, employee.Cnic, employee.DateOfBirth, employee.Gender,
      employee.Nationality, employee.MaritalStatus, employee.BloodGroup);

    if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.Cnic == employee.Cnic, cancellationToken))
      return Result<UpdatedResult>.Failure($"Another employee is registered with CNIC {employee.Cnic}.");

    var changes = EmployeeChangeLogEntry.Diff(before, after);
    var reason = string.IsNullOrWhiteSpace(command.Personal.ChangeReason) ? null : command.Personal.ChangeReason.Trim();
    var needsReason = changes.Where(c => EmployeeFields.NeedReason.Contains(c.Field)).Select(c => c.Field).ToList();
    if (reason is null && needsReason.Count > 0)
      return Result<UpdatedResult>.Failure($"Say why the {(needsReason[0] == EmployeeFields.Cnic ? "CNIC" : "date of birth")} is corrected; the change is kept in the employee's change log.");

    foreach (var (name, oldValue, newValue) in changes)
      log.Add(employee.Id, name, oldValue, newValue, reason);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeEmploymentCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    var (oldType, oldMethod) = (employee.EmploymentType, employee.EmploymentMethod);
    employee.CorrectEmployment(command.EmploymentType, command.EmploymentMethod, command.ProjectId);
    log.Add(employee.Id, EmployeeFields.EmploymentType, EnumText.Label(oldType), EnumText.Label(employee.EmploymentType), command.Reason);
    log.Add(employee.Id, EmployeeFields.EmploymentMethod, EmployeeChangeLogEntry.Words(oldMethod), EmployeeChangeLogEntry.Words(employee.EmploymentMethod), command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectEmployeeNumberCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    var number = Employee.NormalizeNumber(command.EmployeeNumber);

    if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.EmployeeNumber == number, cancellationToken))
      return Result<UpdatedResult>.Failure($"Employee number {number} is already in use.");

    var old = employee.EmployeeNumber;
    employee.CorrectEmployeeNumber(number);
    log.Add(employee.Id, EmployeeFields.EmployeeNumber, old, employee.EmployeeNumber, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetEmployeeTermCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    string? Term(DateOnly? from, DateOnly? to) => from is null && to is null ? null : $"{EmployeeChangeLogEntry.Day(from) ?? "…"} to {EmployeeChangeLogEntry.Day(to) ?? "open"}";
    var (oldTerm, oldDepartment) = (Term(employee.TermFrom, employee.TermTo), employee.LendingDepartment);
    employee.SetTerm(command.TermFrom, command.TermTo, command.LendingDepartment);
    log.Add(employee.Id, EmployeeFields.Term, oldTerm, Term(employee.TermFrom, employee.TermTo), command.Reason);
    log.Add(employee.Id, EmployeeFields.ParentDepartment, oldDepartment, employee.LendingDepartment, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(LinkEmployeeUserCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    var old = employee.UserId;

    if (command.UserId is { } userId)
    {
      if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.UserId == userId, cancellationToken))
        return Result<UpdatedResult>.Failure("This sign-in account is already linked to another employee. Unlink it there first.");
      employee.LinkUser(userId);
    }
    else
    {
      employee.UnlinkUser();
    }

    log.Add(employee.Id, EmployeeFields.SigninAccount, old?.ToString(), employee.UserId?.ToString(), command.UserId is null ? "Unlinked" : "Linked");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetEmployeeProfileStatusCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);

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
    await context.SaveChangesAsync(cancellationToken);
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

  public async Task<Result<GetEmployeeChangeLogQueryResult>> Handle(GetEmployeeChangeLogQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.Id);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.Id} was not found.");

    var rows = await context.ChangeLog.AsNoTracking().Where(c => c.EmployeeId == employeeId)
      .OrderByDescending(c => c.ChangedAt).Take(500).ToListAsync(cancellationToken);
    return Result<GetEmployeeChangeLogQueryResult>.Success(new(rows.Select(c =>
      new EmployeeChangeDto(c.Id.Value, c.Field, c.OldValue, c.NewValue, c.Reason, c.ChangedBy, c.ChangedByName, c.ChangedAt)).ToList()));
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
