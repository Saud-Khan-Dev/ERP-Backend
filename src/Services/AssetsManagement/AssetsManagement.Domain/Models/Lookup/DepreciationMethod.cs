/// STRAIGHT_LINE / DECLINING_BALANCE / UNITS_OF_PRODUCTION ...
public class DepreciationMethod : Aggregate<DepreciationMethodId>
{
  public const string StraightLine = "STRAIGHT_LINE";
  public const string DecliningBalance = "DECLINING_BALANCE";
  public const string UnitsOfProduction = "UNITS_OF_PRODUCTION";

  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsActive { get; private set; }

  public static DepreciationMethod Create(DepreciationMethodId id, LookupCode code, Name name, string? description)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new DepreciationMethod
    {
      Id = id,
      Code = code,
      Name = name,
      Description = description,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, string? description, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    Description = description;
    IsActive = isActive;
  }
}
