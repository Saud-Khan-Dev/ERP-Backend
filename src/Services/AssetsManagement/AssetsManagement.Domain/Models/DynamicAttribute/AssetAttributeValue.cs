using System.Text.Json;

/// Typed projection of asset.extra_attributes for attributes whose resolved assignment is filterable / searchable
/// (or unique per category). NOT hand-written: rebuilt by AttributeValueProjector whenever the JSONB changes.
/// Gives real btree indexes on real types, so "all laptops with ram_gb >= 16" is an index scan.
public class AssetAttributeValue : Entity<AssetAttributeValueId>
{
  public AssetId AssetId { get; private set; } = default!;
  public AttributeDefinitionId AttributeDefinitionId { get; private set; } = default!;
  public string AttributeCode { get; private set; } = default!;

  public string? ValueText { get; private set; }
  public decimal? ValueNumber { get; private set; }
  public bool? ValueBoolean { get; private set; }
  public DateOnly? ValueDate { get; private set; }
  public DateTime? ValueDatetime { get; private set; }
  public JsonElement? ValueJson { get; private set; }
  public OptionSetValueId? OptionValueId { get; private set; }

  /// Position within a MULTISELECT / multi-value attribute.
  public short ValueIndex { get; private set; }

  internal static AssetAttributeValue Create(
      AssetId assetId,
      AttributeDefinitionId definitionId,
      string attributeCode,
      short valueIndex,
      string? valueText,
      decimal? valueNumber,
      bool? valueBoolean,
      DateOnly? valueDate,
      DateTime? valueDatetime,
      JsonElement? valueJson,
      OptionSetValueId? optionValueId)
  {
    return new AssetAttributeValue
    {
      Id = AssetAttributeValueId.Of(Guid.NewGuid()),
      AssetId = assetId,
      AttributeDefinitionId = definitionId,
      AttributeCode = attributeCode,
      ValueIndex = valueIndex,
      ValueText = valueText,
      ValueNumber = valueNumber,
      ValueBoolean = valueBoolean,
      ValueDate = valueDate,
      ValueDatetime = valueDatetime,
      ValueJson = valueJson,
      OptionValueId = optionValueId
    };
  }
}
