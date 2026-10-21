using Microsoft.EntityFrameworkCore;

// Deleting what nobody used (a value or a unit entered by mistake). Anything in use answers 409 with the reason: it is
// deactivated or closed instead, so old records keep their labels.

public sealed record DeleteLoanTypeCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteSalaryComponentCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteWorkShiftCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteOrgUnitCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class DeleteUnusedHandlers(IApplicationDbContext context) :
  ICommandHandler<DeleteLoanTypeCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteSalaryComponentCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteWorkShiftCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteOrgUnitCommand, Result<UpdatedResult>>
{
  public async Task<Result<UpdatedResult>> Handle(DeleteLoanTypeCommand command, CancellationToken cancellationToken)
  {
    var typeId = LoanTypeId.Of(command.Id);
    var type = await context.LoanTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new LoanTypeNotFoundException($"Loan type {command.Id} was not found.");
    if (await context.Loans.AnyAsync(l => l.LoanTypeId == type.Id, cancellationToken))
      throw new InUseException($"Loans of the kind '{type.Name}' were sanctioned, so it cannot be deleted. Deactivate it instead.");
    context.LoanTypes.Remove(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteSalaryComponentCommand command, CancellationToken cancellationToken)
  {
    var component = await context.LoadComponentAsync(command.Id, cancellationToken);
    if (component.IsSystem)
      throw new InUseException($"'{component.ComponentName}' is part of how payroll works; it cannot be deleted.");
    if (await context.SalaryRules.AnyAsync(r => r.SalaryComponentId == component.Id, cancellationToken)
        || await context.SalaryOverrides.AnyAsync(o => o.SalaryComponentId == component.Id, cancellationToken)
        || await context.PayrollLines.AnyAsync(l => l.SalaryComponentId == component.Id, cancellationToken)
        || await context.LoanTypes.AnyAsync(t => t.SalaryComponentId == component.Id, cancellationToken))
      throw new InUseException($"'{component.ComponentName}' has rules, overrides, pay slip lines or a loan type, so it cannot be deleted. Deactivate it instead.");
    context.SalaryComponents.Remove(component);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteWorkShiftCommand command, CancellationToken cancellationToken)
  {
    var shift = await context.LoadShiftAsync(command.Id, cancellationToken);
    if (await context.EmployeeShifts.AnyAsync(s => s.WorkShiftId == shift.Id, cancellationToken)
        || await context.AttendanceRecords.AnyAsync(r => r.WorkShiftId == shift.Id, cancellationToken))
      throw new InUseException($"Employees have worked the shift '{shift.Name}', so it cannot be deleted. Deactivate it instead.");
    context.WorkShifts.Remove(shift);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteOrgUnitCommand command, CancellationToken cancellationToken)
  {
    var unit = await context.LoadOrgUnitAsync(command.Id, cancellationToken);
    var id = unit.Id;
    if (await context.OrganizationUnitVersions.AnyAsync(v => v.ParentUnitId == id, cancellationToken))
      throw new InUseException($"Unit {unit.Code} has units under it, so it cannot be deleted. Close it instead.");
    if (await context.PostVersions.AnyAsync(v => v.OrgUnitId == id, cancellationToken))
      throw new InUseException($"Unit {unit.Code} has posts, so it cannot be deleted. Close it instead.");
    if (await context.ServiceHistory.AnyAsync(h => h.OldOrgUnitId == id || h.NewOrgUnitId == id, cancellationToken)
        || await context.HrActions.AnyAsync(a => a.OldOrgUnitId == id || a.NewOrgUnitId == id, cancellationToken)
        || await context.SalaryRules.AnyAsync(r => r.ApplicableOrgUnitId == id, cancellationToken))
      throw new InUseException($"Service records or salary rules name unit {unit.Code}, so it cannot be deleted. Close it instead.");

    context.OrganizationUnitVersions.RemoveRange(await context.OrganizationUnitVersions.Where(v => v.OrgUnitId == id).ToListAsync(cancellationToken));
    context.OrganizationUnits.Remove(unit);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}
