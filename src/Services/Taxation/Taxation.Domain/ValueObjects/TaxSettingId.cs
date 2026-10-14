public sealed record TaxSettingId : ITypedId<TaxSettingId>
{
  public Guid Value { get; }

  private TaxSettingId(Guid value) => Value = value;

  public static TaxSettingId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Setting id cannot be empty.");

    return new TaxSettingId(value);
  }

  public static TaxSettingId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
