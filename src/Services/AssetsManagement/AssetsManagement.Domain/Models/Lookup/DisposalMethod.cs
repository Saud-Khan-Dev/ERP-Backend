/// SOLD / SCRAPPED / DONATED / WRITTEN_OFF / TRADED_IN ...
public class DisposalMethod : Aggregate<DisposalMethodId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  /// SOLD requires a disposal_value.
  public bool RequiresValue { get; private set; }
  public bool IsActive { get; private set; }

  public static DisposalMethod Create(DisposalMethodId id, LookupCode code, Name name, bool requiresValue)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    return new DisposalMethod
    {
      Id = id,
      Code = code,
      Name = name,
      RequiresValue = requiresValue,
      IsActive = true
    };
  }

  public void Update(LookupCode code, Name name, bool requiresValue, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);

    Code = code;
    Name = name;
    RequiresValue = requiresValue;
    IsActive = isActive;
  }
}
