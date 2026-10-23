using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class AttributeSchemaService(IApplicationDbContext context, IAuditTrail audit) : IAttributeSchemaService
{
  public async Task<IReadOnlyList<AssetCategory>> GetCategoryChainAsync(AssetCategoryId categoryId, CancellationToken cancellationToken)
  {
    var chain = new List<AssetCategory>();
    var visited = new HashSet<Guid>();
    AssetCategoryId? current = categoryId;

    while (current is not null && visited.Add(current.Value))
    {
      var category = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == current, cancellationToken);
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

    // switched-off and "not used here" rows count too: they take a field off the form at their level
    var assignments = await context.AttributeAssignments
        .AsNoTracking()
        .Where(a =>
            (a.Scope == AttributeScope.AssetClass && a.AssetClassId == assetClassId) ||
            (assetTypeId != null && a.Scope == AttributeScope.AssetType && a.AssetTypeId == assetTypeId) ||
            (a.Scope == AttributeScope.Category && a.CategoryId != null && categoryIds.Contains(a.CategoryId)) ||
            (assetId != null && a.Scope == AttributeScope.Asset && a.AssetId == assetId))
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

  public async Task<IReadOnlyDictionary<OptionSetId, OptionSet>> LoadOptionSetsAsync(IEnumerable<OptionSetId> optionSetIds, CancellationToken cancellationToken)
  {
    var ids = optionSetIds.Distinct().ToList();
    if (ids.Count == 0)
      return new Dictionary<OptionSetId, OptionSet>();

    return await context.OptionSets
        .AsNoTracking()
        .Include(o => o.Values)
        .Where(o => ids.Contains(o.Id))
        .ToDictionaryAsync(o => o.Id, cancellationToken);
  }

  public Task<IReadOnlyDictionary<OptionSetId, OptionSet>> LoadOptionSetsAsync(ResolvedAttributeSchema schema, CancellationToken cancellationToken) =>
      LoadOptionSetsAsync(schema.Attributes.Where(a => a.Definition.OptionSetId is not null).Select(a => a.Definition.OptionSetId!), cancellationToken);

  public async Task<ResolvedAttributeSchema> ApplyAttributesAsync(
      Asset asset,
      IReadOnlyDictionary<string, JsonElement> input,
      bool isNewAsset,
      ActorRef changedBy,
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
        optionSetId => optionSets.GetValueOrDefault(optionSetId));

    var projection = AttributeValueProjector.Project(
        asset.Id,
        schema,
        validated,
        (optionSetId, code) => optionSets.TryGetValue(optionSetId, out var set) ? set.FindByCode(code)?.Id : null);

    await EnsureUniquePerCategoryAsync(asset, schema, projection, cancellationToken);

    var definitionsByCode = await DefinitionsForAsync(schema, before.Keys.Union(validated.Keys), cancellationToken);
    var changes = AttributeHistoryDiff.Diff(asset.Id, before, validated, definitionsByCode.ToDictionary(kv => kv.Key, kv => kv.Value.Id),
      DateTime.UtcNow, changedBy, changeReason);

    asset.SetExtraAttributes(validated, DateTime.UtcNow);

    if (!isNewAsset)
    {
      var stale = await context.AssetAttributeValues.Where(v => v.AssetId == asset.Id).ToListAsync(cancellationToken);
      context.AssetAttributeValues.RemoveRange(stale);
    }

    await context.AssetAttributeValues.AddRangeAsync(projection, cancellationToken);

    // the audit trail keeps every field change: one line per field, with its name and the values as a person reads them
    // (choices by their labels). A new asset's values are its first ones ("—" before).
    var otherSets = changes.Select(c => definitionsByCode[c.AttributeCode].OptionSetId)
      .Where(id => id is not null && !optionSets.ContainsKey(id)).Select(id => id!).ToList();
    var allSets = otherSets.Count == 0
      ? optionSets
      : optionSets.Concat(await LoadOptionSetsAsync(otherSets, cancellationToken)).ToDictionary(kv => kv.Key, kv => kv.Value);
    foreach (var change in changes)
    {
      var definition = definitionsByCode[change.AttributeCode];
      var label = schema.Find(change.AttributeCode)?.Label ?? definition.Name.Value;
      var set = definition.OptionSetId is { } setId ? allSets.GetValueOrDefault(setId) : null;
      audit.Line(AuditActions.Changed, AssetAuditKinds.Asset, asset, field: label,
        oldValue: AttributeAuditText.Of(change.OldValue, definition, set), newValue: AttributeAuditText.Of(change.NewValue, definition, set),
        detail: isNewAsset || string.IsNullOrWhiteSpace(changeReason) ? null : changeReason);
    }

    return schema;
  }

  public async Task<int> ReprojectAsync(IReadOnlyCollection<Asset> assets, CancellationToken cancellationToken)
  {
    var count = 0;
    foreach (var asset in assets)
    {
      var schema = await ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, asset.Id, cancellationToken);
      var optionSets = await LoadOptionSetsAsync(schema, cancellationToken);
      var projection = AttributeValueProjector.Project(
          asset.Id, schema, asset.ExtraAttributes,
          (optionSetId, code) => optionSets.TryGetValue(optionSetId, out var set) ? set.FindByCode(code)?.Id : null);

      var stale = await context.AssetAttributeValues.Where(v => v.AssetId == asset.Id).ToListAsync(cancellationToken);
      context.AssetAttributeValues.RemoveRange(stale);
      await context.AssetAttributeValues.AddRangeAsync(projection, cancellationToken);
      count++;
    }
    return count;
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
        throw new DomainException($"Another asset in this category already has this '{attribute.Label}'. It must be different for every asset.");
    }
  }

  /// The definitions of the given codes: the form's, and those of kept values whose field is no longer on it.
  private async Task<IReadOnlyDictionary<string, AttributeDefinition>> DefinitionsForAsync(ResolvedAttributeSchema schema, IEnumerable<string> codes, CancellationToken cancellationToken)
  {
    var map = schema.Attributes.ToDictionary(a => a.Code, a => a.Definition, StringComparer.Ordinal);
    var missing = codes.Where(c => !map.ContainsKey(c)).Distinct().ToList();

    if (missing.Count == 0)
      return map;

    // keys that are not on the form any more (kept values) still get a line when they change
    var attributeCodes = missing.Select(AttributeCode.Of).ToList();
    var extra = await context.AttributeDefinitions
        .AsNoTracking()
        .Where(d => attributeCodes.Contains(d.Code))
        .ToListAsync(cancellationToken);

    foreach (var definition in extra)
      map[definition.Code.Value] = definition;

    return map;
  }
}

/// A field value as a person reads it in the trail: text as it is, yes/no, a date as a date, choices by their labels.
public static class AttributeAuditText
{
  public static string? Of(JsonElement? value, AttributeDefinition definition, OptionSet? choices)
  {
    if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
      return null;

    string Choice(string code) => choices?.FindByCode(code)?.Label ?? code;

    var text = element.ValueKind switch
    {
      JsonValueKind.String when definition.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect => Choice(element.GetString()!),
      JsonValueKind.String => element.GetString(),
      JsonValueKind.True => "Yes",
      JsonValueKind.False => "No",
      JsonValueKind.Number => element.GetRawText(),
      JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
        ? (definition.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect ? Choice(item.GetString()!) : item.GetString())
        : item.GetRawText())),
      _ => element.GetRawText()
    };
    if (string.IsNullOrWhiteSpace(text))
      return null;
    return definition.Unit is { Length: > 0 } unit && element.ValueKind == JsonValueKind.Number
      ? $"{text} {unit}"
      : text;
  }
}
