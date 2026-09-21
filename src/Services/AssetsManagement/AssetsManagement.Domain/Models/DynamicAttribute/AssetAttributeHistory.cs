using System.Text.Json;

/// Who changed which dynamic field, when, from what to what.
public class AssetAttributeHistory : Entity<AssetAttributeHistoryId>
{
  public AssetId AssetId { get; private set; } = default!;
  public AttributeDefinitionId AttributeDefinitionId { get; private set; } = default!;
  public string AttributeCode { get; private set; } = default!;
  public JsonElement? OldValue { get; private set; }
  public JsonElement? NewValue { get; private set; }
  public DateTime ChangedAt { get; private set; }
  public Guid? ChangedBy { get; private set; }
  public string? ChangeReason { get; private set; }

  internal static AssetAttributeHistory Create(
      AssetId assetId,
      AttributeDefinitionId definitionId,
      string attributeCode,
      JsonElement? oldValue,
      JsonElement? newValue,
      DateTime changedAt,
      Guid? changedBy,
      string? changeReason)
  {
    return new AssetAttributeHistory
    {
      Id = AssetAttributeHistoryId.Of(Guid.NewGuid()),
      AssetId = assetId,
      AttributeDefinitionId = definitionId,
      AttributeCode = attributeCode,
      OldValue = oldValue,
      NewValue = newValue,
      ChangedAt = changedAt,
      ChangedBy = changedBy,
      ChangeReason = changeReason
    };
  }
}
