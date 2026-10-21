/// Straight line / declining balance / units of production. The engine works by the method's KIND; the three methods
/// the system starts with are locked (their code and kind cannot change and they cannot be deleted). More methods can
/// be added, each saying which calculation it uses.
public class DepreciationMethod : Aggregate<DepreciationMethodId>
{
  public const string StraightLine = "STRAIGHT_LINE";
  public const string DecliningBalance = "DECLINING_BALANCE";
  public const string UnitsOfProduction = "UNITS_OF_PRODUCTION";

  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public DepreciationMethodKind Kind { get; private set; }
  public bool IsSystem { get; private set; }
  public bool IsActive { get; private set; }

  public static DepreciationMethod Create(DepreciationMethodId id, LookupCode code, Name name, string? description, DepreciationMethodKind kind, bool isSystem = false)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new DepreciationMethod
    {
      Id = id,
      Code = code,
      Name = name,
      Description = description,
      Kind = kind,
      IsSystem = isSystem,
      IsActive = true
    };
  }

  /// `inUse`: a schedule uses it - then its code and calculation stay as they are.
  public void Update(LookupCode code, Name name, string? description, DepreciationMethodKind kind, bool isActive, bool inUse)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    if ((IsSystem || inUse) && code != Code)
      throw new DomainException($"The code of '{Name.Value}' cannot change: {(IsSystem ? "the system relies on it" : "schedules use it")}.");

    if ((IsSystem || inUse) && kind != Kind)
      throw new DomainException($"How '{Name.Value}' is worked out cannot change: {(IsSystem ? "it is one of the system's methods" : "schedules use it")}. Add a new method instead.");

    Code = code;
    Name = name;
    Description = description;
    Kind = kind;
    IsActive = isActive;
  }

  public void EnsureDeletable()
  {
    if (IsSystem)
      throw new DomainException($"'{Name.Value}' is one of the system's methods and cannot be deleted. Switch it off if it is not used.");
  }
}
