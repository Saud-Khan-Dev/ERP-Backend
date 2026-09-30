/// The value one property holds for one custom field.
public class PropertyAttributeValue : Aggregate<PropertyAttributeValueId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public AttributeDefinitionId AttributeDefinitionId { get; private set; } = default!;
  public AttributeDataType DataType { get; private set; }
  public string? Value { get; private set; }

  public static PropertyAttributeValue Create(PropertyAttributeValueId id, Property property, AttributeDefinition definition, string? value)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(definition);
    property.EnsureActive();
    EnsureUsable(definition);
    definition.EnsureValueIsValid(value);

    return new PropertyAttributeValue
    {
      Id = id,
      PropertyId = property.Id,
      AttributeDefinitionId = definition.Id,
      DataType = definition.DataType,
      Value = Normalize(value)
    };
  }

  public void UpdateValue(AttributeDefinition definition, string? value)
  {
    ArgumentNullException.ThrowIfNull(definition);

    if (definition.Id != AttributeDefinitionId)
      throw new DomainException("The field definition does not match the stored value.");

    EnsureUsable(definition);
    definition.EnsureValueIsValid(value);

    DataType = definition.DataType;
    Value = Normalize(value);
  }

  private static void EnsureUsable(AttributeDefinition definition)
  {
    if (!definition.IsActive)
      throw new DomainException($"'{definition.Label.Value}' is no longer available for data entry.");
  }

  private static string? Normalize(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
