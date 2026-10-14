public sealed record PsidSyncId : ITypedId<PsidSyncId>
{
  public Guid Value { get; }

  private PsidSyncId(Guid value) => Value = value;

  public static PsidSyncId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Reference sync id cannot be empty.");

    return new PsidSyncId(value);
  }

  public static PsidSyncId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
