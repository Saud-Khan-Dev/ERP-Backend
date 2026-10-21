using Microsoft.EntityFrameworkCore;

/// The employee record of the signed-in user, for self-service: the token's employee claim (set by Identity when the
/// account is linked to an employee) or, failing that, the employee whose user_id is the caller.
public class CurrentEmployee(IApplicationDbContext context, ICurrentUser currentUser)
{
  public async Task<EmployeeId> IdAsync(CancellationToken cancellationToken) =>
      await TryIdAsync(cancellationToken)
        ?? throw new EmployeeProfileRequiredException("Your account is not linked to an employee record. Ask HR to link it.");

  /// The signed-in account's employee, or null when it is not linked (GET /me/link answers this without a 403).
  public async Task<EmployeeId?> TryIdAsync(CancellationToken cancellationToken)
  {
    if (currentUser.EmployeeId is { } claimed && claimed != Guid.Empty)
    {
      var employeeId = EmployeeId.Of(claimed);
      if (await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
        return employeeId;
    }

    if (currentUser.UserId is { } userId)
    {
      var linked = await context.Employees.Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(cancellationToken);
      if (linked is not null)
        return linked;
    }

    return null;
  }

  public async Task<Employee> LoadAsync(CancellationToken cancellationToken)
  {
    var id = await IdAsync(cancellationToken);
    return await context.Employees.FirstAsync(e => e.Id == id, cancellationToken);
  }
}
