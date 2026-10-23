using Microsoft.EntityFrameworkCore;

/// The employee record of the signed-in user, for self-service: ONLY the token's employee claim (`emp`), which
/// Identity sets when an administrator links the account to an employee. The HR-side link (employee.user_id) is not
/// trusted on its own: an account opens a portal only once Identity has linked it too.
public class CurrentEmployee(IApplicationDbContext context, ICurrentUser currentUser, EmployeeScope scope)
{
  public async Task<EmployeeId> IdAsync(CancellationToken cancellationToken) =>
      await TryIdAsync(cancellationToken)
        ?? throw new EmployeeProfileRequiredException("Your account is not linked to an employee record. Ask HR to link it.");

  /// The signed-in account's employee, or null when it is not linked (GET /me/link answers this without a 403).
  public async Task<EmployeeId?> TryIdAsync(CancellationToken cancellationToken)
  {
    if (currentUser.EmployeeId is not { } claimed || claimed == Guid.Empty)
      return null;

    var employeeId = EmployeeId.Of(claimed);
    // one's own record is never outside one's office scope
    using (scope.Unrestricted())
      return await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken) ? employeeId : null;
  }

  public async Task<Employee> LoadAsync(CancellationToken cancellationToken)
  {
    var id = await IdAsync(cancellationToken);
    using (scope.Unrestricted())
      return await context.Employees.FirstAsync(e => e.Id == id, cancellationToken);
  }
}
