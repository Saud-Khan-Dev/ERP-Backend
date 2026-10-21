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
      throw new DomainException($"The list '{Label.Value}' is one the system relies on and cannot be deleted.");
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
      throw new DomainException($"The list already has the choice '{label.Trim()}'.");

    if (_values.Any(v => string.Equals(v.Label, label.Trim(), StringComparison.OrdinalIgnoreCase)))
      throw new DomainException($"The list already has a choice called '{label.Trim()}'.");

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
      throw new DomainException($"The list already has the choice '{label.Trim()}'.");

    if (_values.Any(v => v.Id != valueId && string.Equals(v.Label, label.Trim(), StringComparison.OrdinalIgnoreCase)))
      throw new DomainException($"The list already has a choice called '{label.Trim()}'.");

    EnsureParentBelongsToSet(parentValueId, valueId);

    optionValue.Update(parentValueId, value, label, color, icon, displayOrder, isActive);
  }

  public void RemoveValue(OptionSetValueId valueId)
  {
    var optionValue = FindValue(valueId);

    if (_values.Any(v => v.ParentValueId == valueId))
      throw new DomainException("Other choices depend on this one. Remove them first.");

    _values.Remove(optionValue);
  }

  public OptionSetValue? FindByCode(string code) =>
      _values.FirstOrDefault(v => string.Equals(v.Value.Value, code, StringComparison.OrdinalIgnoreCase));

  /// Active option codes, as accepted in extra_attributes for SELECT / MULTISELECT attributes.
  public IReadOnlyCollection<string> ActiveCodes() =>
      _values.Where(v => v.IsActive).Select(v => v.Value.Value).ToArray();

  /// The choices a value may take: every active one, plus switched-off ones in `alsoAllowed` (an asset keeps a
  /// choice it already holds after the choice is switched off; it is just not offered any more).
  public IReadOnlyCollection<OptionChoice> Choices(IEnumerable<string> alsoAllowed)
  {
    var kept = new HashSet<string>(alsoAllowed, StringComparer.OrdinalIgnoreCase);
    return _values
      .Where(v => v.IsActive || kept.Contains(v.Value.Value))
      .OrderBy(v => v.DisplayOrder ?? int.MaxValue).ThenBy(v => v.Label, StringComparer.OrdinalIgnoreCase)
      .Select(v => new OptionChoice(v.Value.Value, v.Label, v.IsActive))
      .ToArray();
  }

  private OptionSetValue FindValue(OptionSetValueId valueId) =>
      _values.FirstOrDefault(v => v.Id == valueId)
      ?? throw new DomainException("That choice is not on this list.");

  private void EnsureParentBelongsToSet(OptionSetValueId? parentValueId, OptionSetValueId? self)
  {
    if (parentValueId is null)
      return;

    if (self is not null && parentValueId == self)
      throw new DomainException("A choice cannot depend on itself.");

    if (_values.All(v => v.Id != parentValueId))
      throw new DomainException("A choice can only depend on a choice of the same list.");
  }
}
