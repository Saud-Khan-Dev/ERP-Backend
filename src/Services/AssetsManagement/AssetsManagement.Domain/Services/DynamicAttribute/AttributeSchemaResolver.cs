/// Resolves the effective attribute set of a node of the structure, or of an asset (which sits in a leaf category):
///   CLASS -> TYPE -> CATEGORY (root..leaf) -> ASSET
/// Later levels override earlier ones on the same AttributeDefinitionId. An assignment counts for its own node always,
/// and for the nodes below it only when InheritToChildren is set. A counting assignment that is HIDDEN ("not used here")
/// or switched off takes the field off the form at that level - a lower level may add it back.
public static class AttributeSchemaResolver
{
  public static ResolvedAttributeSchema Resolve(
      IReadOnlyCollection<AttributeAssignment> candidateAssignments,
      AssetClassId assetClassId,
      AssetTypeId? assetTypeId,
      IReadOnlyList<AssetCategory> categoryChainRootToLeaf,
      AssetId? assetId,
      IReadOnlyDictionary<AttributeDefinitionId, AttributeDefinition> definitions)
  {
    ArgumentNullException.ThrowIfNull(candidateAssignments);
    ArgumentNullException.ThrowIfNull(assetClassId);
    ArgumentNullException.ThrowIfNull(categoryChainRootToLeaf);
    ArgumentNullException.ThrowIfNull(definitions);

    // the node the form is for: the leaf category (an asset's form is its category's), else the type, else the class
    var leaf = categoryChainRootToLeaf.Count == 0 ? null : categoryChainRootToLeaf[^1];
    var targetIsClass = leaf is null && assetTypeId is null;
    var targetIsType = leaf is null && assetTypeId is not null;

    var ordered = new List<AttributeAssignment>();

    ordered.AddRange(candidateAssignments.Where(a =>
        a.Scope == AttributeScope.AssetClass && a.AssetClassId == assetClassId && (targetIsClass || a.InheritToChildren)));

    if (assetTypeId is not null)
      ordered.AddRange(candidateAssignments.Where(a =>
          a.Scope == AttributeScope.AssetType && a.AssetTypeId == assetTypeId && (targetIsType || a.InheritToChildren)));

    foreach (var category in categoryChainRootToLeaf)
    {
      var isLeaf = leaf is not null && category.Id == leaf.Id;
      ordered.AddRange(candidateAssignments.Where(a =>
          a.Scope == AttributeScope.Category && a.CategoryId == category.Id && (isLeaf || a.InheritToChildren)));
    }

    if (assetId is not null)
      ordered.AddRange(candidateAssignments.Where(a => a.Scope == AttributeScope.Asset && a.AssetId == assetId));

    var winners = new Dictionary<AttributeDefinitionId, ResolvedAttribute>();
    foreach (var assignment in ordered)
    {
      // "not used here" or switched off at this level: the field is off the form from here down
      if (assignment.IsHidden || !assignment.IsActive)
      {
        winners.Remove(assignment.AttributeDefinitionId);
        continue;
      }

      if (!definitions.TryGetValue(assignment.AttributeDefinitionId, out var definition) || !definition.IsActive)
      {
        winners.Remove(assignment.AttributeDefinitionId);
        continue;
      }

      winners[assignment.AttributeDefinitionId] = new ResolvedAttribute(definition, assignment, assignment.Scope);
    }

    var attributes = winners.Values
        .OrderBy(a => a.Assignment.DisplayOrder ?? int.MaxValue)
        .ThenBy(a => a.Label, StringComparer.OrdinalIgnoreCase)
        .ToList();

    return new ResolvedAttributeSchema(attributes, ordered.Where(a => !a.IsHidden && a.IsActive));
  }
}
