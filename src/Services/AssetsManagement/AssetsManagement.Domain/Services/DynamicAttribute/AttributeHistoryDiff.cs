using System.Text.Json;

/// Produces asset_attribute_history rows for every key whose value changed between two bags.
public static class AttributeHistoryDiff
{
  public static IReadOnlyList<AssetAttributeHistory> Diff(
      AssetId assetId,
      IReadOnlyDictionary<string, JsonElement> before,
      IReadOnlyDictionary<string, JsonElement> after,
      IReadOnlyDictionary<string, AttributeDefinitionId> definitionIdsByCode,
      DateTime changedAt,
      Guid? changedBy,
      string? changeReason)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(before);
    ArgumentNullException.ThrowIfNull(after);
    ArgumentNullException.ThrowIfNull(definitionIdsByCode);

    var rows = new List<AssetAttributeHistory>();

    foreach (var code in before.Keys.Union(after.Keys, StringComparer.Ordinal))
    {
      var hadOld = before.TryGetValue(code, out var oldValue);
      var hasNew = after.TryGetValue(code, out var newValue);

      if (hadOld && hasNew && oldValue.GetRawText() == newValue.GetRawText())
        continue;

      if (!definitionIdsByCode.TryGetValue(code, out var definitionId))
        continue; // definition no longer exists; nothing to attach the history row to

      rows.Add(AssetAttributeHistory.Create(
          assetId,
          definitionId,
          code,
          hadOld ? oldValue.Clone() : null,
          hasNew ? newValue.Clone() : null,
          changedAt,
          changedBy,
          changeReason));
    }

    return rows;
  }
}
