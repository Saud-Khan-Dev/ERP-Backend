/// Sale / scrap / write-off ... Whether proceeds may be recorded, and whether they must be.
public class DisposalMethod : Aggregate<DisposalMethodId>
{
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  /// The proceeds must be entered (a sale).
  public bool RequiresValue { get; private set; }
  /// The proceeds may be entered (scrap sold for its scrap value); off for a write-off.
  public bool AllowsValue { get; private set; }
  public bool IsActive { get; private set; }

  public static DisposalMethod Create(DisposalMethodId id, LookupCode code, Name name, bool requiresValue, bool allowsValue = true)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    Check(requiresValue, allowsValue);

    return new DisposalMethod
    {
      Id = id,
      Code = code,
      Name = name,
      RequiresValue = requiresValue,
      AllowsValue = allowsValue,
      IsActive = true
    };
  }

  /// `inUse`: a disposal record uses it - then its code stays as it is.
  public void Update(LookupCode code, Name name, bool requiresValue, bool allowsValue, bool isActive, bool inUse)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    Check(requiresValue, allowsValue);

    if (inUse && code != Code)
      throw new DomainException($"'{Name.Value}' is used by disposals, so its code cannot change.");

    Code = code;
    Name = name;
    RequiresValue = requiresValue;
    AllowsValue = allowsValue;
    IsActive = isActive;
  }

  private static void Check(bool requiresValue, bool allowsValue)
  {
    if (requiresValue && !allowsValue)
      throw new DomainException("A method that requires the proceeds must also allow them.");
  }
}
