public sealed record OrganizationUnitVersionId : ITypedId<OrganizationUnitVersionId>
{
  public Guid Value { get; }

  private OrganizationUnitVersionId(Guid value) => Value = value;

  public static OrganizationUnitVersionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Organization unit version id cannot be empty.");

    return new OrganizationUnitVersionId(value);
  }

  public static OrganizationUnitVersionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
