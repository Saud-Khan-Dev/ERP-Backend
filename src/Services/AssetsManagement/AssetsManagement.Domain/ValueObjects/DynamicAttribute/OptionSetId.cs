public sealed record OptionSetId
{
  public Guid Value { get; }

  private OptionSetId(Guid value) => Value = value;

  public static OptionSetId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Option set Id cannot be empty");

    return new OptionSetId(value);
  }
}
