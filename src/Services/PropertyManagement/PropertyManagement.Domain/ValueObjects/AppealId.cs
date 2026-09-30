public sealed record AppealId
{
  public Guid Value { get; }

  private AppealId(Guid value) => Value = value;

  public static AppealId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Appeal id cannot be empty.");

    return new AppealId(value);
  }

  public static AppealId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
