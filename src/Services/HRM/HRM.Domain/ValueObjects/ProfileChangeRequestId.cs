public sealed record ProfileChangeRequestId : ITypedId<ProfileChangeRequestId>
{
  public Guid Value { get; }

  private ProfileChangeRequestId(Guid value) => Value = value;

  public static ProfileChangeRequestId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Profile change id cannot be empty.");

    return new ProfileChangeRequestId(value);
  }

  public static ProfileChangeRequestId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
