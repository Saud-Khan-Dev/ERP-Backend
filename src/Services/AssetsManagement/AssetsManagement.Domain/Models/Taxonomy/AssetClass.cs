/// Level 1 of the taxonomy. Admin-defined: PHYSICAL / FINANCIAL / INTANGIBLE / DIGITAL ...
public class AssetClass : Aggregate<AssetClassId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static AssetClass Create(AssetClassId id, LookupCode code, Name name, string? description, int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new AssetClass
    {
      Id = id,
      Code = code,
      Name = name,
      Description = description,
      DisplayOrder = displayOrder,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, string? description, int? displayOrder, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    Description = description;
    DisplayOrder = displayOrder;
    IsActive = isActive;
  }

  public void Activate() => IsActive = true;
  public void Deactivate() => IsActive = false;
}
