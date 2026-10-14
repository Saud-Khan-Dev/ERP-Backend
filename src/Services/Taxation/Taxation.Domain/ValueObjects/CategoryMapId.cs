public sealed record CategoryMapId : ITypedId<CategoryMapId>
{
  public Guid Value { get; }

  private CategoryMapId(Guid value) => Value = value;

  public static CategoryMapId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Category map id cannot be empty.");

    return new CategoryMapId(value);
  }

  public static CategoryMapId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
