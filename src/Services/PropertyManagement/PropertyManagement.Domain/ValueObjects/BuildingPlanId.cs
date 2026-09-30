public sealed record BuildingPlanId
{
  public Guid Value { get; }

  private BuildingPlanId(Guid value) => Value = value;

  public static BuildingPlanId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Building plan id cannot be empty.");

    return new BuildingPlanId(value);
  }

  public static BuildingPlanId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
