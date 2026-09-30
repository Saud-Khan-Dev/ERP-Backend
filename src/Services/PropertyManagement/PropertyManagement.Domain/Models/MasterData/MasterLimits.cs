/// Column sizes of each master table, as the ERD defines them: most are code varchar(30) / name
/// varchar(100); town, measurement_unit and contact_type differ.
public static class MasterLimits
{
  public sealed record Limits(int CodeLength, int NameLength);

  private static readonly Limits Standard = new(30, 100);

  private static readonly Dictionary<Type, Limits> Overrides = new()
  {
    [typeof(Town)] = new(20, 150),
    [typeof(MeasurementUnit)] = new(20, 50),
    [typeof(ContactType)] = new(20, 50),
  };

  public static Limits For(Type masterType) => Overrides.GetValueOrDefault(masterType, Standard);
}
