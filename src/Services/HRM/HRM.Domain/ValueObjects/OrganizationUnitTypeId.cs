public sealed record OrganizationUnitTypeId : ITypedId<OrganizationUnitTypeId>
{
  public Guid Value { get; }

  private OrganizationUnitTypeId(Guid value) => Value = value;

  public static OrganizationUnitTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Organization unit type id cannot be empty.");

    return new OrganizationUnitTypeId(value);
  }

  public static OrganizationUnitTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
