using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// The reusable field dictionary. Defined ONCE, assigned to many scopes via AttributeAssignment.
///   manufacturer      TEXT
///   ram_gb            INTEGER  unit: GB  min: 1  max: 1024
///   operating_system  SELECT   option_set: OPERATING_SYSTEM
public class AttributeDefinition : Aggregate<AttributeDefinitionId>
{
  private const int DefaultTextMaxLength = 4000;

  public AttributeCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public AttributeDataType DataType { get; private set; }

  public OptionSetId? OptionSetId { get; private set; }
  public string? ReferenceEntity { get; private set; }

  public string? Unit { get; private set; }
  public int? NumericPrecision { get; private set; }
  public int? NumericScale { get; private set; }

  // ---- declarative validation ----
  public decimal? MinNumber { get; private set; }
  public decimal? MaxNumber { get; private set; }
  public int? MinLength { get; private set; }
  public int? MaxLength { get; private set; }
  public DateOnly? MinDate { get; private set; }
  public DateOnly? MaxDate { get; private set; }
  public string? RegexPattern { get; private set; }
  public bool IsUniquePerCategory { get; private set; }
  public string? ValidationMessage { get; private set; }

  public bool IsMultiValue { get; private set; }
  public bool IsPii { get; private set; }
  public bool IsSystem { get; private set; }
  public bool IsActive { get; private set; }

  public bool RequiresOptionSet => DataType is AttributeDataType.Select or AttributeDataType.MultiSelect;

  public static AttributeDefinition Create(
      AttributeDefinitionId id,
      AttributeCode code,
      Name name,
      string? description,
      AttributeDataType dataType,
      OptionSetId? optionSetId,
      string? referenceEntity,
      string? unit,
      int? numericPrecision,
      int? numericScale,
      AttributeValidationRules rules,
      bool isMultiValue,
      bool isPii,
      bool isSystem)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    var definition = new AttributeDefinition
    {
      Id = id,
      Code = code,
      Name = name,
      IsSystem = isSystem,
      IsActive = true
    };

    definition.Apply(name, description, dataType, optionSetId, referenceEntity, unit, numericPrecision, numericScale, rules, isMultiValue, isPii);

    return definition;
  }

  public void Update(
      Name name,
      string? description,
      AttributeDataType dataType,
      OptionSetId? optionSetId,
      string? referenceEntity,
      string? unit,
      int? numericPrecision,
      int? numericScale,
      AttributeValidationRules rules,
      bool isMultiValue,
      bool isPii,
      bool isActive)
  {
    ArgumentNullException.ThrowIfNull(name);

    Apply(name, description, dataType, optionSetId, referenceEntity, unit, numericPrecision, numericScale, rules, isMultiValue, isPii);
    IsActive = isActive;
  }

  /// The code is the JSONB key. The application layer only calls this when no asset holds a value yet.
  public void Rename(AttributeCode code)
  {
    ArgumentNullException.ThrowIfNull(code);
    Code = code;
  }

  public void Activate() => IsActive = true;
  public void Deactivate() => IsActive = false;

  public void EnsureDeletable()
  {
    if (IsSystem)
      throw new DomainException($"Attribute '{Code.Value}' is a system attribute and cannot be deleted.");
  }

  private void Apply(
      Name name,
      string? description,
      AttributeDataType dataType,
      OptionSetId? optionSetId,
      string? referenceEntity,
      string? unit,
      int? numericPrecision,
      int? numericScale,
      AttributeValidationRules rules,
      bool isMultiValue,
      bool isPii)
  {
    ArgumentNullException.ThrowIfNull(rules);

    var requiresOptionSet = dataType is AttributeDataType.Select or AttributeDataType.MultiSelect;

    if (requiresOptionSet && optionSetId is null)
      throw new DomainException($"{dataType} attributes require an option set.");

    if (dataType == AttributeDataType.Reference && string.IsNullOrWhiteSpace(referenceEntity))
      throw new DomainException("REFERENCE attributes must declare the referenced entity (EMPLOYEE / SUPPLIER / ASSET / DEPARTMENT).");

    if (rules.MinNumber.HasValue && rules.MaxNumber.HasValue && rules.MinNumber > rules.MaxNumber)
      throw new DomainException("min_number cannot be greater than max_number.");

    if (rules.MinLength.HasValue && rules.MaxLength.HasValue && rules.MinLength > rules.MaxLength)
      throw new DomainException("min_length cannot be greater than max_length.");

    if (rules.MinDate.HasValue && rules.MaxDate.HasValue && rules.MinDate > rules.MaxDate)
      throw new DomainException("min_date cannot be greater than max_date.");

    if ((rules.MinLength < 0) || (rules.MaxLength < 0))
      throw new DomainException("Length limits cannot be negative.");

    if (!string.IsNullOrWhiteSpace(rules.RegexPattern))
    {
      try { _ = new Regex(rules.RegexPattern); }
      catch (ArgumentException) { throw new DomainException("regex_pattern is not a valid regular expression."); }
    }

    if (numericScale.HasValue && numericPrecision.HasValue && numericScale > numericPrecision)
      throw new DomainException("numeric_scale cannot exceed numeric_precision.");

    if (isMultiValue && requiresOptionSet)
      throw new DomainException("Use MULTISELECT instead of is_multi_value for option lists.");

    Name = name;
    Description = description;
    DataType = dataType;
    OptionSetId = requiresOptionSet ? optionSetId : null;
    ReferenceEntity = dataType == AttributeDataType.Reference ? referenceEntity!.Trim().ToUpperInvariant() : null;
    Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    NumericPrecision = numericPrecision;
    NumericScale = numericScale;
    MinNumber = rules.MinNumber;
    MaxNumber = rules.MaxNumber;
    MinLength = rules.MinLength;
    MaxLength = rules.MaxLength;
    MinDate = rules.MinDate;
    MaxDate = rules.MaxDate;
    RegexPattern = string.IsNullOrWhiteSpace(rules.RegexPattern) ? null : rules.RegexPattern;
    IsUniquePerCategory = rules.IsUniquePerCategory;
    ValidationMessage = string.IsNullOrWhiteSpace(rules.ValidationMessage) ? null : rules.ValidationMessage.Trim();
    IsMultiValue = isMultiValue;
    IsPii = isPii;
  }

  // =====================================================
  // VALUE VALIDATION
  // =====================================================

  /// Validates a raw JSON value against this definition and returns its canonical form
  /// (numbers as numbers, option codes upper-cased, dates as ISO strings ...).
  /// allowedOptions: active option codes of the linked option set (SELECT / MULTISELECT only).
  public JsonElement NormalizeValue(JsonElement value, string label, IReadOnlyCollection<string>? allowedOptions)
  {
    if (DataType == AttributeDataType.MultiSelect)
    {
      if (value.ValueKind != JsonValueKind.Array)
        Fail(label, "must be a list of option codes.");

      var selected = value.EnumerateArray()
          .Select(item => NormalizeSelect(item, label, allowedOptions))
          .Distinct(StringComparer.Ordinal)
          .ToArray();

      return JsonSerializer.SerializeToElement(selected);
    }

    if (IsMultiValue)
    {
      if (value.ValueKind != JsonValueKind.Array)
        Fail(label, "must be a list of values.");

      var items = value.EnumerateArray().Select(item => NormalizeScalar(item, label, allowedOptions)).ToArray();
      return JsonSerializer.SerializeToElement(items);
    }

    if (value.ValueKind == JsonValueKind.Array && DataType != AttributeDataType.Json)
      Fail(label, "does not accept a list of values.");

    return NormalizeScalar(value, label, allowedOptions);
  }

  private JsonElement NormalizeScalar(JsonElement value, string label, IReadOnlyCollection<string>? allowedOptions)
  {
    switch (DataType)
    {
      case AttributeDataType.Text:
      case AttributeDataType.LongText:
      {
        var text = ReadString(value, label);
        var max = MaxLength ?? (DataType == AttributeDataType.Text ? DefaultTextMaxLength : int.MaxValue);
        if (text.Length > max) Fail(label, $"cannot exceed {max} characters.");
        if (MinLength.HasValue && text.Length < MinLength) Fail(label, $"must be at least {MinLength} characters.");
        if (RegexPattern is not null && !Regex.IsMatch(text, RegexPattern)) Fail(label, "does not match the required format.");
        return JsonSerializer.SerializeToElement(text);
      }

      case AttributeDataType.Integer:
      {
        long number;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out number)) { }
        else if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) { }
        else { Fail(label, "must be a whole number."); return default; }

        CheckRange(number, label);
        return JsonSerializer.SerializeToElement(number);
      }

      case AttributeDataType.Decimal:
      {
        decimal number;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out number)) { }
        else if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)) { }
        else { Fail(label, "must be a decimal number."); return default; }

        CheckRange(number, label);
        if (NumericScale.HasValue) number = Math.Round(number, NumericScale.Value, MidpointRounding.AwayFromZero);
        return JsonSerializer.SerializeToElement(number);
      }

      case AttributeDataType.Boolean:
      {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
          return JsonSerializer.SerializeToElement(value.GetBoolean());
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
          return JsonSerializer.SerializeToElement(parsed);
        Fail(label, "must be either true or false.");
        return default;
      }

      case AttributeDataType.Date:
      {
        var text = ReadString(value, label);
        if (!DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && !(DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) && (date = DateOnly.FromDateTime(dt)) != default))
          Fail(label, "must be a valid date (yyyy-MM-dd).");
        if (MinDate.HasValue && date < MinDate) Fail(label, $"cannot be before {MinDate:yyyy-MM-dd}.");
        if (MaxDate.HasValue && date > MaxDate) Fail(label, $"cannot be after {MaxDate:yyyy-MM-dd}.");
        return JsonSerializer.SerializeToElement(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
      }

      case AttributeDataType.DateTime:
      {
        var text = ReadString(value, label);
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment))
          Fail(label, "must be a valid date/time (ISO 8601).");
        var date = DateOnly.FromDateTime(moment.UtcDateTime);
        if (MinDate.HasValue && date < MinDate) Fail(label, $"cannot be before {MinDate:yyyy-MM-dd}.");
        if (MaxDate.HasValue && date > MaxDate) Fail(label, $"cannot be after {MaxDate:yyyy-MM-dd}.");
        return JsonSerializer.SerializeToElement(moment.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
      }

      case AttributeDataType.Select:
        return JsonSerializer.SerializeToElement(NormalizeSelect(value, label, allowedOptions));

      case AttributeDataType.Json:
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
          Fail(label, "must contain a JSON value.");
        return value.Clone();

      case AttributeDataType.Reference:
      case AttributeDataType.File:
      {
        var text = ReadString(value, label);
        if (!Guid.TryParse(text, out var reference) || reference == Guid.Empty)
          Fail(label, DataType == AttributeDataType.File ? "must be the id of an uploaded attachment." : $"must be the id of a {ReferenceEntity}.");
        return JsonSerializer.SerializeToElement(reference.ToString());
      }

      default:
        throw new DomainException($"Unsupported attribute data type {DataType}.");
    }
  }

  private string NormalizeSelect(JsonElement value, string label, IReadOnlyCollection<string>? allowedOptions)
  {
    var code = ReadString(value, label);
    var match = allowedOptions?.FirstOrDefault(o => string.Equals(o, code, StringComparison.OrdinalIgnoreCase));

    if (match is null)
      Fail(label, allowedOptions is { Count: > 0 }
          ? $"must be one of: {string.Join(", ", allowedOptions)}."
          : "has no active options configured.");

    return match!;
  }

  private void CheckRange(decimal number, string label)
  {
    if (MinNumber.HasValue && number < MinNumber) Fail(label, $"must be at least {MinNumber}.");
    if (MaxNumber.HasValue && number > MaxNumber) Fail(label, $"cannot exceed {MaxNumber}.");
  }

  private string ReadString(JsonElement value, string label)
  {
    if (value.ValueKind == JsonValueKind.String)
      return value.GetString()!.Trim();

    if (value.ValueKind == JsonValueKind.Number)
      return value.GetRawText();

    Fail(label, "must be a text value.");
    return string.Empty;
  }

  private void Fail(string label, string rule) =>
      throw new DomainException(ValidationMessage ?? $"'{label}' {rule}");
}

/// Declarative validation bundle for AttributeDefinition (keeps Create/Update signatures readable).
public sealed record AttributeValidationRules(
    decimal? MinNumber = null,
    decimal? MaxNumber = null,
    int? MinLength = null,
    int? MaxLength = null,
    DateOnly? MinDate = null,
    DateOnly? MaxDate = null,
    string? RegexPattern = null,
    bool IsUniquePerCategory = false,
    string? ValidationMessage = null);
