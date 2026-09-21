/// Reusable dropdown list. Define "Operating System" once, reuse it on Laptop, Desktop, Server, VM.
public class OptionSet : Aggregate<OptionSetId>
{
  private readonly List<OptionSetValue> _values = new();

  public LookupCode Code { get; private set; } = default!;
  public Name Label { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsSystem { get; private set; }
  public bool IsActive { get; private set; }
  public IReadOnlyList<OptionSetValue> Values => _values.AsReadOnly();

  public static OptionSet Create(OptionSetId id, LookupCode code, Name label, string? description, bool isSystem)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(label);

    return new OptionSet
    {
      Id = id,
      Code = code,
      Label = label,
      Description = description,
      IsSystem = isSystem,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name label, string? description, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(label);

    Code = code;
    Label = label;
    Description = description;
    IsActive = isActive;
  }

  public void EnsureDeletable()
  {
    if (IsSystem)
      throw new DomainException($"Option set '{Code.Value}' is a system set and cannot be deleted.");
  }

  public OptionSetValue AddValue(
      OptionSetValueId valueId,
      OptionSetValueId? parentValueId,
      LookupCode value,
      string label,
      string? color,
      string? icon,
      int? displayOrder)
  {
    if (_values.Any(v => v.Value == value))
      throw new DomainException($"Option set '{Code.Value}' already contains the value '{value.Value}'.");

    EnsureParentBelongsToSet(parentValueId, null);

    var optionValue = OptionSetValue.Create(valueId, Id, parentValueId, value, label, color, icon, displayOrder);
    _values.Add(optionValue);
    return optionValue;
  }

  public void UpdateValue(
      OptionSetValueId valueId,
      OptionSetValueId? parentValueId,
      LookupCode value,
      string label,
      string? color,
      string? icon,
      int? displayOrder,
      bool isActive)
  {
    var optionValue = FindValue(valueId);

    if (_values.Any(v => v.Id != valueId && v.Value == value))
      throw new DomainException($"Option set '{Code.Value}' already contains the value '{value.Value}'.");

    EnsureParentBelongsToSet(parentValueId, valueId);

    optionValue.Update(parentValueId, value, label, color, icon, displayOrder, isActive);
  }

  public void RemoveValue(OptionSetValueId valueId)
  {
    var optionValue = FindValue(valueId);

    if (_values.Any(v => v.ParentValueId == valueId))
      throw new DomainException("This option value has child values. Remove them first.");

    _values.Remove(optionValue);
  }

  public OptionSetValue? FindByCode(string code) =>
      _values.FirstOrDefault(v => string.Equals(v.Value.Value, code, StringComparison.OrdinalIgnoreCase));

  /// Active option codes, as accepted in extra_attributes for SELECT / MULTISELECT attributes.
  public IReadOnlyCollection<string> ActiveCodes() =>
      _values.Where(v => v.IsActive).Select(v => v.Value.Value).ToArray();

  private OptionSetValue FindValue(OptionSetValueId valueId) =>
      _values.FirstOrDefault(v => v.Id == valueId)
      ?? throw new DomainException($"Option value {valueId.Value} does not belong to option set '{Code.Value}'.");

  private void EnsureParentBelongsToSet(OptionSetValueId? parentValueId, OptionSetValueId? self)
  {
    if (parentValueId is null)
      return;

    if (self is not null && parentValueId == self)
      throw new DomainException("An option value cannot be its own parent.");

    if (_values.All(v => v.Id != parentValueId))
      throw new DomainException("The parent option value must belong to the same option set.");
  }
}
