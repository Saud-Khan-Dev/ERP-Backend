/// DRAFT / ACTIVE / IN_MAINTENANCE / IN_TRANSIT / DISPOSED ... Admin-defined.
public class AssetStatus : Aggregate<AssetStatusId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  /// Terminal statuses block further edits.
  public bool IsTerminal { get; private set; }
  public bool AllowsAssignment { get; private set; }
  public string? Color { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static AssetStatus Create(AssetStatusId id, LookupCode code, Name name, string? description, bool isTerminal, bool allowsAssignment, string? color, int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new AssetStatus
    {
      Id = id,
      Code = code,
      Name = name,
      Description = description,
      IsTerminal = isTerminal,
      AllowsAssignment = allowsAssignment,
      Color = color,
      DisplayOrder = displayOrder,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, string? description, bool isTerminal, bool allowsAssignment, string? color, int? displayOrder, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    Description = description;
    IsTerminal = isTerminal;
    AllowsAssignment = allowsAssignment;
    Color = color;
    DisplayOrder = displayOrder;
    IsActive = isActive;
  }
}
