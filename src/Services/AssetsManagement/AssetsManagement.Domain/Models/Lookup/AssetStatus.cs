/// In service / In store / Under repair / Disposed ... Admin-defined. Its two flags decide what can happen to an
/// asset in it, so they are fixed once any asset or history line uses the status (add a new status instead).
public class AssetStatus : Aggregate<AssetStatusId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  /// Final statuses close the record (reached only through a disposal).
  public bool IsTerminal { get; private set; }
  public bool AllowsAssignment { get; private set; }
  /// Kept for the database; screens colour a status by what it means (its flags), never by a free colour.
  public string? Color { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static AssetStatus Create(AssetStatusId id, LookupCode code, Name name, string? description, bool isTerminal, bool allowsAssignment, string? color, int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if (isTerminal && allowsAssignment)
      throw new DomainException("A final status cannot allow moves: the record is closed in it.");

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

  /// `inUse`: an asset (deleted ones too) or a history line refers to it - then its code and behaviour stay as they are.
  public void Update(LookupCode code, Name name, string? description, bool isTerminal, bool allowsAssignment, string? color, int? displayOrder, bool isActive, bool inUse)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if (inUse && (isTerminal != IsTerminal || allowsAssignment != AllowsAssignment))
      throw new DomainException($"'{Name.Value}' is used by assets, so what it allows cannot change - it would change those assets too. Add a new status instead.");

    if (inUse && code != Code)
      throw new DomainException($"'{Name.Value}' is used by assets, so its code cannot change.");

    if (isTerminal && allowsAssignment)
      throw new DomainException("A final status cannot allow moves: the record is closed in it.");

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
