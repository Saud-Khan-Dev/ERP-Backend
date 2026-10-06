using Microsoft.EntityFrameworkCore;

public class EmployeeHandlers(
  IApplicationDbContext context,
  CodeIssuer codes,
  ServiceRecordWriter writer,
  FileUploads files,
  ICurrentUser currentUser,
  IClock clock) :
  ICommandHandler<RegisterEmployeeCommand, Result<RegisterEmployeeCommandResult>>,
  ICommandHandler<UpdatePersonalDetailsCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeEmploymentCommand, Result<UpdatedResult>>,
  ICommandHandler<CorrectEmployeeNumberCommand, Result<UpdatedResult>>,
  ICommandHandler<LinkEmployeeUserCommand, Result<UpdatedResult>>,
  ICommandHandler<SetEmployeeProfileStatusCommand, Result<UpdatedResult>>,
  ICommandHandler<UploadEmployeePhotoCommand, Result<UpdatedResult>>
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
        appointment.ExternalReferenceOrg, null, appointment.Remarks, currentUser.UserId), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<RegisterEmployeeCommandResult>.Success(new(employee.Id.Value, employee.EmployeeNumber));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePersonalDetailsCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    employee.UpdatePersonalDetails(command.Personal.ToDetails(), clock.Today);

    if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.Cnic == employee.Cnic, cancellationToken))
      return Result<UpdatedResult>.Failure($"Another employee is registered with CNIC {employee.Cnic}.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeEmploymentCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    employee.ChangeEmployment(command.EmploymentType, command.EmploymentMethod, command.ProjectId);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(CorrectEmployeeNumberCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);
    var number = Employee.NormalizeNumber(command.EmployeeNumber);

    if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.EmployeeNumber == number, cancellationToken))
      return Result<UpdatedResult>.Failure($"Employee number {number} is already in use.");

    employee.CorrectEmployeeNumber(number);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(LinkEmployeeUserCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.Id, cancellationToken);

    if (command.UserId is { } userId)
    {
      if (await context.Employees.AnyAsync(e => e.Id != employee.Id && e.UserId == userId, cancellationToken))
        return Result<UpdatedResult>.Failure("This user account is already linked to another employee.");
      employee.LinkUser(userId);
    }
    else
    {
      employee.UnlinkUser();
    }

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

    employee.SetProfileStatus(command.Status);
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
