public sealed record DisposalMethodId
{
  public Guid Value { get; }

  private DisposalMethodId(Guid value) => Value = value;

  public static DisposalMethodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Disposal method Id cannot be empty");

    return new DisposalMethodId(value);
  }
}
