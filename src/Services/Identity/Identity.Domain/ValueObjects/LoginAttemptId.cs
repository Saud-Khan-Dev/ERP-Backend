public sealed record LoginAttemptId
{
  public Guid Value { get; }

  private LoginAttemptId(Guid value) => Value = value;

  public static LoginAttemptId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Login attempt Id cannot be empty");

    return new LoginAttemptId(value);
  }
}
