using Microsoft.EntityFrameworkCore;

/// The employee's identity, employment and account changes, written to the audit trail with the reason (old -> new,
/// why, who, when). The handlers that change these facts save quietly (`IAuditTrail.Quietly`) so the trail holds this
/// one line per fact, with its reason, rather than a second line without it. The old employee_change_log table is kept
/// read-only (its rows were copied into the trail).
public class ChangeLog(IApplicationDbContext context, IAuditTrail audit)
{
  /// Records a change; nothing when the value did not change.
  public void Add(EmployeeId employeeId, string field, string? oldValue, string? newValue, string? reason = null)
  {
    if (string.Equals(oldValue ?? "", newValue ?? "", StringComparison.Ordinal))
      return;

    var detail = string.IsNullOrWhiteSpace(reason) ? null : $"Reason: {reason.Trim()}";
    var employee = context.Employees.Local.FirstOrDefault(e => e.Id == employeeId);
    if (employee is not null)
      audit.Record(AuditActions.Changed, employee, detail, field, oldValue, newValue);
    else
      audit.Record(AuditActions.Changed, "Employee", employeeId.Value, parentId: employeeId.Value, detail: detail, field: field, oldValue: oldValue, newValue: newValue);
  }
}
