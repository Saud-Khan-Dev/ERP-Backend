/// Resolves the effective attribute set of an asset:
///   CLASS -> TYPE -> CATEGORY (root..leaf) -> ASSET
/// Later levels override earlier ones on the same AttributeDefinitionId.
/// Category assignments on ancestors only apply when inherit_to_children is set; the leaf's own always apply.
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

    var active = candidateAssignments.Where(a => a.IsActive).ToList();
    var ordered = new List<AttributeAssignment>();

    ordered.AddRange(active.Where(a => a.Scope == AttributeScope.AssetClass && a.AssetClassId == assetClassId));

    if (assetTypeId is not null)
      ordered.AddRange(active.Where(a => a.Scope == AttributeScope.AssetType && a.AssetTypeId == assetTypeId));

    var leaf = categoryChainRootToLeaf.Count == 0 ? null : categoryChainRootToLeaf[^1];
    foreach (var category in categoryChainRootToLeaf)
    {
      var isLeaf = leaf is not null && category.Id == leaf.Id;
      ordered.AddRange(active.Where(a =>
          a.Scope == AttributeScope.Category
          && a.CategoryId == category.Id
          && (isLeaf || a.InheritToChildren)));
    }

    if (assetId is not null)
      ordered.AddRange(active.Where(a => a.Scope == AttributeScope.Asset && a.AssetId == assetId));

    var winners = new Dictionary<AttributeDefinitionId, ResolvedAttribute>();
    foreach (var assignment in ordered)
    {
      if (!definitions.TryGetValue(assignment.AttributeDefinitionId, out var definition) || !definition.IsActive)
        continue;

      winners[assignment.AttributeDefinitionId] = new ResolvedAttribute(definition, assignment, assignment.Scope);
    }

    var attributes = winners.Values
        .OrderBy(a => a.Assignment.DisplayOrder ?? int.MaxValue)
        .ThenBy(a => a.Label, StringComparer.OrdinalIgnoreCase)
        .ToList();

    return new ResolvedAttributeSchema(attributes, ordered);
  }
}
