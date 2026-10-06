public sealed record LeaveEntitlementId : ITypedId<LeaveEntitlementId>
{
  public Guid Value { get; }

  private LeaveEntitlementId(Guid value) => Value = value;

  public static LeaveEntitlementId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Leave entitlement id cannot be empty.");

    return new LeaveEntitlementId(value);
  }

  public static LeaveEntitlementId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
