public sealed record OptionSetValueId
{
  public Guid Value { get; }

  private OptionSetValueId(Guid value) => Value = value;

  public static OptionSetValueId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Option set value Id cannot be empty");

    return new OptionSetValueId(value);
  }
}
