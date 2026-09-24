public sealed record PermissionId
{
  public Guid Value { get; }

  private PermissionId(Guid value) => Value = value;

  public static PermissionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Permission Id cannot be empty");

    return new PermissionId(value);
  }
}
