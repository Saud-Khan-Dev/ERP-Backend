using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class AttributeSchemaService(IApplicationDbContext context) : IAttributeSchemaService
{
  public async Task<IReadOnlyList<AssetCategory>> GetCategoryChainAsync(AssetCategoryId categoryId, CancellationToken cancellationToken)
  {
    var chain = new List<AssetCategory>();
    var visited = new HashSet<Guid>();
    AssetCategoryId? current = categoryId;

    while (current is not null && visited.Add(current.Value))
    {
      var category = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == current, cancellationToken);
      if (category is null)
        break;

      chain.Add(category);
      current = category.ParentCategoryId;
    }

    chain.Reverse();
    return chain;
  }

  public async Task<ResolvedAttributeSchema> ResolveAsync(
      AssetClassId assetClassId,
      AssetTypeId? assetTypeId,
      AssetCategoryId? categoryId,
      AssetId? assetId,
      CancellationToken cancellationToken)
  {
    var chain = categoryId is null
        ? Array.Empty<AssetCategory>()
        : await GetCategoryChainAsync(categoryId, cancellationToken);

    var categoryIds = chain.Select(c => c.Id).ToList();

    var assignments = await context.AttributeAssignments
        .AsNoTracking()
        .Where(a => a.IsActive && (
            (a.Scope == AttributeScope.AssetClass && a.AssetClassId == assetClassId) ||
            (assetTypeId != null && a.Scope == AttributeScope.AssetType && a.AssetTypeId == assetTypeId) ||
            (a.Scope == AttributeScope.Category && a.CategoryId != null && categoryIds.Contains(a.CategoryId)) ||
            (assetId != null && a.Scope == AttributeScope.Asset && a.AssetId == assetId)))
        .ToListAsync(cancellationToken);

    if (assignments.Count == 0)
      return ResolvedAttributeSchema.Empty;

    var definitionIds = assignments.Select(a => a.AttributeDefinitionId).Distinct().ToList();

    var definitions = await context.AttributeDefinitions
        .AsNoTracking()
        .Where(d => definitionIds.Contains(d.Id))
        .ToDictionaryAsync(d => d.Id, cancellationToken);

    return AttributeSchemaResolver.Resolve(assignments, assetClassId, assetTypeId, chain, assetId, definitions);
  }

  public async Task<IReadOnlyDictionary<OptionSetId, OptionSet>> LoadOptionSetsAsync(ResolvedAttributeSchema schema, CancellationToken cancellationToken)
  {
    var optionSetIds = schema.Attributes
        .Where(a => a.Definition.OptionSetId is not null)
        .Select(a => a.Definition.OptionSetId!)
        .Distinct()
        .ToList();

    if (optionSetIds.Count == 0)
      return new Dictionary<OptionSetId, OptionSet>();

    return await context.OptionSets
        .AsNoTracking()
        .Include(o => o.Values)
        .Where(o => optionSetIds.Contains(o.Id))
        .ToDictionaryAsync(o => o.Id, cancellationToken);
  }

  public async Task<ResolvedAttributeSchema> ApplyAttributesAsync(
      Asset asset,
      IReadOnlyDictionary<string, JsonElement> input,
      bool isNewAsset,
      Guid? changedBy,
      string? changeReason,
      CancellationToken cancellationToken)
  {
    var schema = await ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, asset.Id, cancellationToken);
    var optionSets = await LoadOptionSetsAsync(schema, cancellationToken);

    var before = isNewAsset
        ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        : asset.ExtraAttributes.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    var validated = AttributeValueValidator.Validate(
        schema,
        input,
        isNewAsset ? null : before,
        optionSetId => optionSets.TryGetValue(optionSetId, out var set) ? set.ActiveCodes() : null);

    var projection = AttributeValueProjector.Project(
        asset.Id,
        schema,
        validated,
        (optionSetId, code) => optionSets.TryGetValue(optionSetId, out var set) ? set.FindByCode(code)?.Id : null);

    await EnsureUniquePerCategoryAsync(asset, schema, projection, cancellationToken);

    var definitionIdsByCode = await DefinitionIdsForAsync(schema, before.Keys.Union(validated.Keys), cancellationToken);
    var history = AttributeHistoryDiff.Diff(asset.Id, before, validated, definitionIdsByCode, DateTime.UtcNow, changedBy, changeReason);

    asset.SetExtraAttributes(validated, DateTime.UtcNow);

    if (!isNewAsset)
    {
      var stale = await context.AssetAttributeValues.Where(v => v.AssetId == asset.Id).ToListAsync(cancellationToken);
      context.AssetAttributeValues.RemoveRange(stale);
    }

    await context.AssetAttributeValues.AddRangeAsync(projection, cancellationToken);
    await context.AssetAttributeHistories.AddRangeAsync(history, cancellationToken);

    return schema;
  }

  /// is_unique_per_category: e.g. serial_number must not repeat inside the same category.
  private async Task EnsureUniquePerCategoryAsync(Asset asset, ResolvedAttributeSchema schema, IReadOnlyList<AssetAttributeValue> projection, CancellationToken cancellationToken)
  {
    foreach (var attribute in schema.Attributes.Where(a => a.Definition.IsUniquePerCategory))
    {
      var definitionId = attribute.Definition.Id;
      var texts = projection.Where(p => p.AttributeDefinitionId == definitionId && p.ValueText != null).Select(p => p.ValueText!).ToList();
      if (texts.Count == 0)
        continue;

      var duplicate = await context.AssetAttributeValues
          .Join(context.Assets, v => v.AssetId, a => a.Id, (v, a) => new { Value = v, Asset = a })
          .AnyAsync(x =>
              x.Asset.CategoryId == asset.CategoryId
              && x.Asset.Id != asset.Id
              && x.Value.AttributeDefinitionId == definitionId
              && texts.Contains(x.Value.ValueText!), cancellationToken);

      if (duplicate)
        throw new DomainException($"'{attribute.Label}' must be unique within the category; another asset already uses this value.");
    }
  }

  private async Task<IReadOnlyDictionary<string, AttributeDefinitionId>> DefinitionIdsForAsync(ResolvedAttributeSchema schema, IEnumerable<string> codes, CancellationToken cancellationToken)
  {
    var map = schema.Attributes.ToDictionary(a => a.Code, a => a.Definition.Id, StringComparer.Ordinal);
    var missing = codes.Where(c => !map.ContainsKey(c)).Distinct().ToList();

    if (missing.Count == 0)
      return map;

    // keys that dropped out of the schema (e.g. after re-classification) still get a history row
    var attributeCodes = missing.Select(AttributeCode.Of).ToList();
    var extra = await context.AttributeDefinitions
        .AsNoTracking()
        .Where(d => attributeCodes.Contains(d.Code))
        .ToListAsync(cancellationToken);

    foreach (var definition in extra)
      map[definition.Code.Value] = definition.Id;

    return map;
  }
}
