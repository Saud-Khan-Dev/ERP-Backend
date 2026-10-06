/// Configurable catalogue of unit types (Authority / Department / Wing / Division / Section / Office).
/// hierarchy_level lives only here; a unit's depth comes from its parent chain.
public class OrganizationUnitType : Aggregate<OrganizationUnitTypeId>
{
  public const int NameMaxLength = 100;
  public const int CodeMaxLength = 30;

  public string Name { get; private set; } = default!;
  public string Code { get; private set; } = default!;
  public int? HierarchyLevel { get; private set; }
  public bool IsActive { get; private set; }

  public static OrganizationUnitType Create(OrganizationUnitTypeId id, string name, string code, int? hierarchyLevel)
  {
    var type = new OrganizationUnitType { Id = id, IsActive = true };
    type.Update(name, code, hierarchyLevel);
    return type;
  }

  public void Update(string name, string code, int? hierarchyLevel)
  {
    Name = Guard.RequiredText(name, NameMaxLength, "Unit type name");
    Code = Guard.Code(code, CodeMaxLength, "Unit type code");
    HierarchyLevel = hierarchyLevel is < 0 ? throw new DomainException("Hierarchy level cannot be negative.") : hierarchyLevel;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Unit type '{Name}' is inactive.");
  }
}
