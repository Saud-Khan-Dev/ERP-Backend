using System.Globalization;

/// A custom field an administrator adds to the property form, for facts the schema has no column for.
/// Grouped by an admin-editable attribute_group master.
public class AttributeDefinition : Aggregate<AttributeDefinitionId>
{
  public const int MaxTextLength = 4000;

  public MasterId AttributeGroupId { get; private set; } = default!;
  /// Stable key of the field: PLOT_FACING, ELECTRICITY_METER_NO ...
  public MasterCode Code { get; private set; } = default!;
  public Name Label { get; private set; } = default!;
  public AttributeDataType DataType { get; private set; }
  public bool IsRequired { get; private set; }
  public string? OptionsCsv { get; private set; }
  public string? DefaultValue { get; private set; }
  public int DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  public IReadOnlyList<string> Options =>
      string.IsNullOrWhiteSpace(OptionsCsv)
          ? Array.Empty<string>()
          : OptionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

  public static AttributeDefinition Create(
      AttributeDefinitionId id,
      AttributeGroup group,
      MasterCode code,
      Name label,
      AttributeDataType dataType,
      bool isRequired,
      string? optionsCsv,
      string? defaultValue,
      int displayOrder)
  {
    ArgumentNullException.ThrowIfNull(code);

    var definition = new AttributeDefinition { Id = id, Code = code, IsActive = true };
    definition.Apply(group, label, dataType, isRequired, optionsCsv, defaultValue, displayOrder);
    return definition;
  }

  /// The code is immutable: values and reports refer to it.
  public void Update(
      AttributeGroup group,
      Name label,
      AttributeDataType dataType,
      bool isRequired,
      string? optionsCsv,
      string? defaultValue,
      int displayOrder) =>
      Apply(group, label, dataType, isRequired, optionsCsv, defaultValue, displayOrder);

  public void Activate() => IsActive = true;
  public void Deactivate() => IsActive = false;

  /// Validates a raw data-entry value against the configured data type.
  public void EnsureValueIsValid(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      if (IsRequired)
        throw new DomainException($"'{Label.Value}' is required.");

      return;
    }

    value = value.Trim();

    switch (DataType)
    {
      case AttributeDataType.Text:
        if (value.Length > MaxTextLength)
          throw new DomainException($"'{Label.Value}' cannot exceed {MaxTextLength} characters.");
        break;

      case AttributeDataType.Number:
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
          throw new DomainException($"'{Label.Value}' must be a whole number.");
        break;

      case AttributeDataType.Decimal:
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
          throw new DomainException($"'{Label.Value}' must be a decimal number.");
        break;

      case AttributeDataType.Date:
        if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
          throw new DomainException($"'{Label.Value}' must be a valid date (yyyy-MM-dd).");
        break;

      case AttributeDataType.DateTime:
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
          throw new DomainException($"'{Label.Value}' must be a valid date and time.");
        break;

      case AttributeDataType.Boolean:
        if (!bool.TryParse(value, out _))
          throw new DomainException($"'{Label.Value}' must be either true or false.");
        break;

      case AttributeDataType.Dropdown:
        if (!Options.Contains(value, StringComparer.OrdinalIgnoreCase))
          throw new DomainException($"'{Label.Value}' must be one of: {OptionsCsv}.");
        break;

      case AttributeDataType.MultiSelect:
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
          if (!Options.Contains(item, StringComparer.OrdinalIgnoreCase))
            throw new DomainException($"'{Label.Value}' contains an invalid option '{item}'. Allowed: {OptionsCsv}.");
        break;
    }
  }

  private void Apply(
      AttributeGroup group,
      Name label,
      AttributeDataType dataType,
      bool isRequired,
      string? optionsCsv,
      string? defaultValue,
      int displayOrder)
  {
    ArgumentNullException.ThrowIfNull(group);
    ArgumentNullException.ThrowIfNull(label);

    if (group.Id != AttributeGroupId)
      group.EnsureActive();

    if (!Enum.IsDefined(dataType))
      throw new DomainException("Unknown data type.");

    if (label.Value.Length > 100)
      throw new DomainException("Label cannot exceed 100 characters.");

    AttributeGroupId = group.Id;
    Label = label;
    DataType = dataType;
    IsRequired = isRequired;
    OptionsCsv = NormalizeOptions(dataType, optionsCsv);
    DisplayOrder = displayOrder;

    DefaultValue = null;
    if (!string.IsNullOrWhiteSpace(defaultValue))
    {
      EnsureValueIsValid(defaultValue);
      DefaultValue = defaultValue.Trim();
    }
  }

  private static string? NormalizeOptions(AttributeDataType dataType, string? optionsCsv)
  {
    if (dataType is not (AttributeDataType.Dropdown or AttributeDataType.MultiSelect))
      return null;

    var options = (optionsCsv ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (options.Length == 0)
      throw new DomainException($"{dataType} fields need a list of options.");

    var csv = string.Join(',', options);
    if (csv.Length > 2000)
      throw new DomainException("The option list cannot exceed 2000 characters.");

    return csv;
  }
}
