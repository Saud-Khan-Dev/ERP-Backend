public sealed record ClearanceId : ITypedId<ClearanceId>
{
  public Guid Value { get; }

  private ClearanceId(Guid value) => Value = value;

  public static ClearanceId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Clearance id cannot be empty.");

    return new ClearanceId(value);
  }

  public static ClearanceId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
