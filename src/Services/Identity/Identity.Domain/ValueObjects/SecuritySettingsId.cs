public sealed record SecuritySettingsId
{
  public Guid Value { get; }

  private SecuritySettingsId(Guid value) => Value = value;

  public static SecuritySettingsId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Security Settings Id cannot be empty");

    return new SecuritySettingsId(value);
  }
}
