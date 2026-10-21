using System.Text.Json;

/// Application-side orchestration of the dynamic attribute engine (the ERD's asset triggers, in C#).
public interface IAttributeSchemaService
{
  /// Ancestors first, the requested category last.
  Task<IReadOnlyList<AssetCategory>> GetCategoryChainAsync(AssetCategoryId categoryId, CancellationToken cancellationToken);

  /// Resolves the effective attribute set for CLASS -> TYPE -> CATEGORY chain -> ASSET.
  Task<ResolvedAttributeSchema> ResolveAsync(AssetClassId assetClassId, AssetTypeId? assetTypeId, AssetCategoryId? categoryId, AssetId? assetId, CancellationToken cancellationToken);

  /// Option sets (with values) used by the schema, keyed by option set id.
  Task<IReadOnlyDictionary<OptionSetId, OptionSet>> LoadOptionSetsAsync(ResolvedAttributeSchema schema, CancellationToken cancellationToken);

  /// Option sets (with values) by id.
  Task<IReadOnlyDictionary<OptionSetId, OptionSet>> LoadOptionSetsAsync(IEnumerable<OptionSetId> optionSetIds, CancellationToken cancellationToken);

  /// Validates `input` (a patch over the asset's current bag), stores the canonical bag on the asset, rebuilds the
  /// typed projection rows and writes history diffs. Nothing is saved: the caller owns SaveChangesAsync.
  Task<ResolvedAttributeSchema> ApplyAttributesAsync(
      Asset asset,
      IReadOnlyDictionary<string, JsonElement> input,
      bool isNewAsset,
      ActorRef changedBy,
      string? changeReason,
      CancellationToken cancellationToken);

  /// Rebuilds the typed rows filters and search read (after a field's Filter or Search switch changed). Not saved.
  Task<int> ReprojectAsync(IReadOnlyCollection<Asset> assets, CancellationToken cancellationToken);
}
