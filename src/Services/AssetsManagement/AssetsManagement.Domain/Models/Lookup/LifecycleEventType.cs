/// What can appear on an asset's timeline. SYSTEM types are written by the acts themselves (registered, moved, issued,
/// returned, status changed, disposed …) and are found by their PURPOSE, never by code: their code, stage and purpose
/// are fixed and they cannot be switched off or deleted (their name can change). Every other type is recorded by hand
/// (an inspection, a repair) and has no purpose.
///
/// Stage: ACQUISITION / ASSIGNMENT / MAINTENANCE / DEPRECIATION / VALUATION / DISPOSAL / RECORD.
public class LifecycleEventType : Aggregate<LifecycleEventTypeId>
{
  public string? Stage { get; private set; }
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public EventPurpose? Purpose { get; private set; }
  public bool IsSystem { get; private set; }
  public bool IsActive { get; private set; }

  /// A clerk records it through "Record event"; system types are written by their act.
  public bool IsHandRecorded => Purpose is null;

  public static LifecycleEventType Create(LifecycleEventTypeId id, string? stage, LookupCode code, Name name, string? description)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new LifecycleEventType
    {
      Id = id,
      Stage = NormalizeStage(stage),
      Code = code,
      Name = name,
      Description = description,
      IsActive = true
    };
  }

  public static LifecycleEventType CreateSystem(LifecycleEventTypeId id, EventPurpose purpose, string stage, LookupCode code, Name name, string? description) =>
      new()
      {
        Id = id,
        Stage = NormalizeStage(stage),
        Code = code,
        Name = name,
        Description = description,
        Purpose = purpose,
        IsSystem = true,
        IsActive = true
      };

  /// `inUse`: events of this type are on record - then its code stays as it is.
  public void Update(string? stage, LookupCode code, Name name, string? description, bool isActive, bool inUse)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if (IsSystem)
    {
      if (code != Code || NormalizeStage(stage) != Stage)
        throw new DomainException($"'{Name.Value}' is written by the system itself; only its name and description can change.");
      if (!isActive)
        throw new DomainException($"'{Name.Value}' is written by the system itself and cannot be switched off.");
    }

    if (inUse && code != Code)
      throw new DomainException($"'{Name.Value}' is on asset timelines, so its code cannot change.");

    Stage = NormalizeStage(stage);
    Code = code;
    Name = name;
    Description = description;
    IsActive = isActive;
  }

  public void EnsureDeletable()
  {
    if (IsSystem)
      throw new DomainException($"'{Name.Value}' is written by the system itself and cannot be deleted.");
  }

  private static string? NormalizeStage(string? stage) =>
      string.IsNullOrWhiteSpace(stage) ? null : stage.Trim().ToUpperInvariant();
}
