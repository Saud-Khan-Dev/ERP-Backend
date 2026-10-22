/// Raw SQL of the AuditTrail migration (2026-10-21): the employee change log's rows are copied into the shared audit
/// trail, so an employee's History is one list from the start. The old table stays, read-only (its trigger already
/// refuses changes); nothing writes to it any more.
public static class AuditTrailMigrationSql
{
  public const string CopyChangeLog = """
    INSERT INTO hrms.audit_entry (id, at, module, actor_id, actor_name, action, record_kind, record_id, record_code, record_label,
      parent_id, parent_code, field, old_value, new_value, detail, ip_address, device, request_id)
    SELECT c.id, c.changed_at::timestamptz, 'Human Resources', c.changed_by,
      COALESCE(NULLIF(c.changed_by_name, ''), CASE WHEN c.changed_by IS NULL THEN 'System' ELSE c.changed_by::text END),
      'Changed', 'Employee', c.employee_id, e.employee_number, e.full_name, c.employee_id, NULL,
      c.field, LEFT(c.old_value, 4000), LEFT(c.new_value, 4000),
      CASE WHEN c.reason IS NULL OR c.reason = '' THEN 'Copied from the employee change log' ELSE 'Reason: ' || c.reason END,
      NULL, NULL, NULL
    FROM hrms.employee_change_log c
    LEFT JOIN hrms.employee e ON e.id = c.employee_id
    WHERE NOT EXISTS (SELECT 1 FROM hrms.audit_entry a WHERE a.id = c.id);
    """;
}
