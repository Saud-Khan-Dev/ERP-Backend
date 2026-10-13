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

  /// An individual with a CNIC of their own (an INDIVIDUAL needs one): 35202-000000n-1.
  public static PropertyOwner Owner(string name = "Owner")
  {
    var number = ++_owners;
    return PropertyOwner.Create(OwnerId.New(), BusinessCode.Of($"OWN-{number:00000}"), Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual),
      Name.Of(name), null, Cnic.Of($"35202{number:0000000}1"), null, null, null);
  }

  /// A company (no CNIC, identified by its NTN).
  public static PropertyOwner Company(string name = "Company Ltd", string? ntn = null) =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of($"OWN-{++_owners:00000}"), Master<OwnerType>(SystemMasterCodes.OwnerTypeCompany),
        Name.Of(name), null, null, Ntn.OfNullable(ntn), null, null);

  public static DateOnly D(string iso) => DateOnly.Parse(iso);
}
