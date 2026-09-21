/// One entry of a reusable dropdown list, e.g. WINDOWS_11 / "Windows 11".
/// ParentValueId enables cascading dropdowns (Country -> City).
public class OptionSetValue : Entity<OptionSetValueId>
{
  public OptionSetId OptionSetId { get; private set; } = default!;
  public OptionSetValueId? ParentValueId { get; private set; }
  public LookupCode Value { get; private set; } = default!;
  public string Label { get; private set; } = default!;
  public string? Color { get; private set; }
  public string? Icon { get; private set; }
  public int? DisplayOrder { get; private set; }
  public bool IsActive { get; private set; }

  internal static OptionSetValue Create(
      OptionSetValueId id,
      OptionSetId optionSetId,
      OptionSetValueId? parentValueId,
      LookupCode value,
      string label,
      string? color,
      string? icon,
      int? displayOrder)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentException.ThrowIfNullOrWhiteSpace(label);

    return new OptionSetValue
    {
      Id = id,
      OptionSetId = optionSetId,
      ParentValueId = parentValueId,
      Value = value,
      Label = label.Trim(),
      Color = color,
      Icon = icon,
      DisplayOrder = displayOrder,
      IsActive = true
    };
  }

  internal void Update(OptionSetValueId? parentValueId, LookupCode value, string label, string? color, string? icon, int? displayOrder, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentException.ThrowIfNullOrWhiteSpace(label);

    ParentValueId = parentValueId;
    Value = value;
    Label = label.Trim();
    Color = color;
    Icon = icon;
    DisplayOrder = displayOrder;
    IsActive = isActive;
  }
}
