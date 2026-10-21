/// Writes the employee change log (old -> new, why, who, when) inside the caller's save.
public class ChangeLog(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
{
  /// Records a change; nothing when the value did not change.
  public void Add(EmployeeId employeeId, string field, string? oldValue, string? newValue, string? reason = null)
  {
    if (string.Equals(oldValue ?? "", newValue ?? "", StringComparison.Ordinal))
      return;

    context.ChangeLog.Add(EmployeeChangeLogEntry.Record(employeeId, field, oldValue, newValue, reason, currentUser.UserId,
      currentUser.IsAuthenticated ? currentUser.AuditName : null, clock.UtcNow));
  }
}
