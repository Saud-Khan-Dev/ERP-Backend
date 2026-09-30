/// Builders for the objects most tests need: master values, a property, owners.
internal static class Fixture
{
  public static T Master<T>(string code, MasterExtras? extras = null) where T : MasterData, new() =>
      MasterData.Create<T>(MasterId.New(), MasterCode.Of(code), Name.Of(code), null, 0, extras);

  public static MeasurementUnit Unit(string code, decimal factor, bool isBase = false) =>
      Master<MeasurementUnit>(code, new MasterExtras(FactorToBase: factor, IsBase: isBase));

  public static Property Property(string code = "PROP-00001", PropertyStatus? status = null) =>
      global::Property.Create(
        PropertyId.New(), BusinessCode.Of(code), Name.Of("Test Plaza"),
        Master<Town>("TOWN_A"), Master<PropertyType>("COMMERCIAL"), status ?? Master<PropertyStatus>("OCCUPIED"),
        Master<PropertyClassification>("GDA_PROPERTY"), null, null, null, null);

  private static int _owners;

  public static PropertyOwner Owner(string name = "Owner") =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of($"OWN-{++_owners:00000}"), Master<OwnerType>("INDIVIDUAL"),
        Name.Of(name), null, null, null, null, null, null);

  public static DateOnly D(string iso) => DateOnly.Parse(iso);
}
