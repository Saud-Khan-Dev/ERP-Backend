/// One line of a property's (or an owner's) change log: who changed which fact of which record, from what to what.
/// Written by the save itself (never by a handler), so nothing that changes a record escapes it; never edited.
public class PropertyChange
{
  public Guid Id { get; private set; }
  /// The property the changed record hangs off (null for an owner's own record).
  public Guid? PropertyId { get; private set; }
  /// The owner whose own record changed (their CNIC, name ...); null otherwise.
  public Guid? OwnerId { get; private set; }
  /// The kind of record in words ("Lease", "Ownership") and its code when it has one (LSE-00012).
  public string Entity { get; private set; } = default!;
  public Guid EntityId { get; private set; }
  public string? EntityCode { get; private set; }
  /// Added, Changed or Removed.
  public string Action { get; private set; } = default!;
  /// The fact in words ("Lease end date"); null for an added or removed record.
  public string? Field { get; private set; }
  public string? OldValue { get; private set; }
  public string? NewValue { get; private set; }
  /// The signed-in account (Identity user id) and its name as it stood.
  public Guid? ChangedBy { get; private set; }
  public string? ChangedByName { get; private set; }
  public DateTime ChangedAt { get; private set; }

  public const int TextMaxLength = 1000;

  public static PropertyChange Log(Guid? propertyId, Guid? ownerId, string entity, Guid entityId, string? entityCode, string action,
      string? field, string? oldValue, string? newValue, Guid? changedBy, string? changedByName, DateTime changedAt) => new()
  {
    Id = Guid.NewGuid(),
    PropertyId = propertyId,
    OwnerId = ownerId,
    Entity = entity,
    EntityId = entityId,
    EntityCode = entityCode,
    Action = action,
    Field = field,
    OldValue = Clip(oldValue),
    NewValue = Clip(newValue),
    ChangedBy = changedBy,
    ChangedByName = changedByName,
    ChangedAt = changedAt
  };

  private static string? Clip(string? value) =>
      string.IsNullOrEmpty(value) ? null : value.Length <= TextMaxLength ? value : value[..(TextMaxLength - 1)] + "…";
}
