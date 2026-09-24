public sealed record SessionId
{
  public Guid Value { get; }

  private SessionId(Guid value) => Value = value;

  public static SessionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Session Id cannot be empty");

    return new SessionId(value);
  }
}
