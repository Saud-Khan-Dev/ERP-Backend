/// The parent links of the org units on one date, to keep the structure a tree.
public static class OrganizationTree
{
  /// Refuses a parent that is the unit itself or one of its descendants (which would make a loop).
  /// `parentOf` = each unit's parent on the date the change takes effect.
  public static void EnsureNoCycle(OrganizationUnitId unitId, OrganizationUnitId? newParentId, IReadOnlyDictionary<OrganizationUnitId, OrganizationUnitId?> parentOf)
  {
    var visited = new HashSet<OrganizationUnitId>();
    for (var current = newParentId; current is not null; current = parentOf.GetValueOrDefault(current))
    {
      if (current == unitId)
        throw new DomainException("A unit cannot be placed under itself or one of its own sub-units.");

      if (!visited.Add(current))
        break; // an existing loop elsewhere is not this change's doing
    }
  }
}
