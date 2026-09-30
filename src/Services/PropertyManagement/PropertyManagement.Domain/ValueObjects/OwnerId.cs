public sealed record OwnerId
{
  public Guid Value { get; }

  private OwnerId(Guid value) => Value = value;

  public static OwnerId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Owner id cannot be empty.");

    return new OwnerId(value);
  }

  public static OwnerId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
