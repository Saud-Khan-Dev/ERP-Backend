/// taxation.audit_log: who changed what and when - every saved change to the configuration (circles, heads, rate sets,
/// settings, policies, rules, templates, letterhead, notice table) and to the decisions on files and money (file details,
/// category and status, overrides, holds, opening arrears, fees, clearances, payments and their reversal, run approvals),
/// with the values before and after. Written by the save itself, so nothing can change without its row.
public class AuditLog : Entity<AuditLogId>
{
  public string EntityName { get; private set; } = default!;
  public Guid EntityId { get; private set; }
  /// ADDED, MODIFIED or DELETED.
  public string Action { get; private set; } = default!;
  public string? OldValues { get; private set; }
  public string? NewValues { get; private set; }
  public Guid? UserId { get; private set; }
  public string? UserName { get; private set; }
  public DateTime At { get; private set; }
  /// The tax file the change belongs to, when it belongs to one (its history lists it).
  public Guid? TaxFileId { get; private set; }

  private AuditLog() { }

  public static AuditLog Record(string entity, Guid entityId, string action, string? oldValues, string? newValues, Guid? userId, string? userName, DateTime at, Guid? taxFileId) => new()
  {
    Id = AuditLogId.New(),
    EntityName = entity.Length <= 60 ? entity : entity[..60],
    EntityId = entityId,
    Action = action,
    OldValues = oldValues,
    NewValues = newValues,
    UserId = userId,
    UserName = userName is null ? null : userName.Length <= 100 ? userName : userName[..100],
    At = at,
    TaxFileId = taxFileId
  };
}
