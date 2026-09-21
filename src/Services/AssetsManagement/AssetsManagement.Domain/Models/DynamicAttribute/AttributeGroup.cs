/// Purely for UI layout: "Hardware", "Warranty", "Finance".
public class AttributeGroup : Aggregate<AttributeGroupId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsCollapsible { get; private set; }
  public bool IsActive { get; private set; }

  public static AttributeGroup Create(AttributeGroupId id, LookupCode code, Name name, string? description, int? displayOrder, bool isCollapsible)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new AttributeGroup
    {
      Id = id,
      Code = code,
      Name = name,
      Description = description,
      DisplayOrder = displayOrder,
      IsCollapsible = isCollapsible,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, string? description, int? displayOrder, bool isCollapsible, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    Description = description;
    DisplayOrder = displayOrder;
    IsCollapsible = isCollapsible;
    IsActive = isActive;
  }
}
