using System.Globalization;
using System.Text.Json;

/// Builds the typed asset_attribute_value rows from a validated extra_attributes bag
/// (the ERD's AFTER INSERT/UPDATE projection trigger, in C#). Only projected attributes get rows.
public static class AttributeValueProjector
{
  public static IReadOnlyList<AssetAttributeValue> Project(
      AssetId assetId,
      ResolvedAttributeSchema schema,
      IReadOnlyDictionary<string, JsonElement> values,
      Func<OptionSetId, string, OptionSetValueId?> resolveOptionValue)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(schema);
    ArgumentNullException.ThrowIfNull(values);
    ArgumentNullException.ThrowIfNull(resolveOptionValue);

    var rows = new List<AssetAttributeValue>();

    foreach (var attribute in schema.Attributes)
    {
      if (!attribute.IsProjected || !values.TryGetValue(attribute.Code, out var value))
        continue;

      var definition = attribute.Definition;
      var isList = value.ValueKind == JsonValueKind.Array && definition.DataType != AttributeDataType.Json;
      var items = isList ? value.EnumerateArray().ToList() : new List<JsonElement> { value };

      for (short index = 0; index < items.Count; index++)
        rows.Add(ProjectScalar(assetId, attribute, items[index], index, resolveOptionValue));
    }

    return rows;
  }

  private static AssetAttributeValue ProjectScalar(
      AssetId assetId,
      ResolvedAttribute attribute,
      JsonElement value,
      short index,
      Func<OptionSetId, string, OptionSetValueId?> resolveOptionValue)
  {
    var definition = attribute.Definition;
    string? text = null;
    decimal? number = null;
    bool? boolean = null;
    DateOnly? date = null;
    DateTime? dateTime = null;
    JsonElement? json = null;
    OptionSetValueId? optionValueId = null;

    switch (definition.DataType)
    {
      case AttributeDataType.Integer:
      case AttributeDataType.Decimal:
        number = value.GetDecimal();
        text = number.Value.ToString(CultureInfo.InvariantCulture);
        break;

      case AttributeDataType.Boolean:
        boolean = value.GetBoolean();
        text = boolean.Value ? "true" : "false";
        break;

      case AttributeDataType.Date:
        date = DateOnly.Parse(value.GetString()!, CultureInfo.InvariantCulture);
        text = date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        break;

      case AttributeDataType.DateTime:
        dateTime = DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        dateTime = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
        text = dateTime.Value.ToString("O", CultureInfo.InvariantCulture);
        break;

      case AttributeDataType.Select:
      case AttributeDataType.MultiSelect:
        text = value.GetString();
        optionValueId = definition.OptionSetId is { } setId && text is not null ? resolveOptionValue(setId, text) : null;
        break;

      case AttributeDataType.Json:
        json = value.Clone();
        text = value.GetRawText();
        break;

      default:
        text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        break;
    }

    return AssetAttributeValue.Create(
        assetId,
        definition.Id,
        definition.Code.Value,
        index,
        text,
        number,
        boolean,
        date,
        dateTime,
        json,
        optionValueId);
  }
}
