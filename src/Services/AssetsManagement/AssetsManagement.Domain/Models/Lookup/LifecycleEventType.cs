/// Stage: ACQUISITION / ASSIGNMENT / MAINTENANCE / DEPRECIATION / VALUATION / DISPOSAL.
public class LifecycleEventType : Aggregate<LifecycleEventTypeId>
{
  public string? Stage { get; private set; }
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsActive { get; private set; }

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

  public void Update(string? stage, LookupCode code, Name name, string? description, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Stage = NormalizeStage(stage);
    Code = code;
    Name = name;
    Description = description;
    IsActive = isActive;
  }

  private static string? NormalizeStage(string? stage) =>
      string.IsNullOrWhiteSpace(stage) ? null : stage.Trim().ToUpperInvariant();
}
