public sealed record PermissionModuleId
{
  public Guid Value { get; }

  private PermissionModuleId(Guid value) => Value = value;

  public static PermissionModuleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Permission module Id cannot be empty");

    return new PermissionModuleId(value);
  }
}
