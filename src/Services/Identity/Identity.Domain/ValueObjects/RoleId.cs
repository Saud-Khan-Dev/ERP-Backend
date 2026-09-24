public sealed record RoleId
{
  public Guid Value { get; }

  private RoleId(Guid value) => Value = value;

  public static RoleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Role Id cannot be empty");

    return new RoleId(value);
  }
}
