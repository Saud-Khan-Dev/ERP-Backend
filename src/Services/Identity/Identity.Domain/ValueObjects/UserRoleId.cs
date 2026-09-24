public sealed record UserRoleId
{
  public Guid Value { get; }

  private UserRoleId(Guid value) => Value = value;

  public static UserRoleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("User role Id cannot be empty");

    return new UserRoleId(value);
  }
}
