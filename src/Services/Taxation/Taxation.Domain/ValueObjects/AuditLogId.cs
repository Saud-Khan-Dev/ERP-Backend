public sealed record AuditLogId : ITypedId<AuditLogId>
{
  public Guid Value { get; }

  private AuditLogId(Guid value) => Value = value;

  public static AuditLogId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Audit log id cannot be empty.");

    return new AuditLogId(value);
  }

  public static AuditLogId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
