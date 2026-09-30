public class OwnerAddress : Entity<OwnerAddressId>
{
  public const string DefaultCountry = "Pakistan";

  public OwnerId OwnerId { get; private set; } = default!;
  public AddressType AddressType { get; private set; }
  public string FullAddress { get; private set; } = default!;
  public string? CityTown { get; private set; }
  public string? District { get; private set; }
  public string? Province { get; private set; }
  public string Country { get; private set; } = DefaultCountry;
  public string? PostalCode { get; private set; }
  public bool IsPrimary { get; private set; }
  public bool IsActive { get; private set; }

  internal static OwnerAddress Create(
      OwnerAddressId id,
      OwnerId ownerId,
      AddressType addressType,
      string fullAddress,
      string? cityTown,
      string? district,
      string? province,
      string? country,
      string? postalCode,
      bool isPrimary)
  {
    var address = new OwnerAddress { Id = id, OwnerId = ownerId, IsActive = true };
    address.Update(addressType, fullAddress, cityTown, district, province, country, postalCode, isPrimary);
    return address;
  }

  internal void Update(
      AddressType addressType,
      string fullAddress,
      string? cityTown,
      string? district,
      string? province,
      string? country,
      string? postalCode,
      bool isPrimary)
  {
    if (!IsActive)
      throw new DomainException("This address is inactive.");

    if (!Enum.IsDefined(addressType))
      throw new DomainException("Unknown address type.");

    AddressType = addressType;
    FullAddress = Guard.RequiredText(fullAddress, 400, "Full address");
    CityTown = Guard.Text(cityTown, 100, "City / town");
    District = Guard.Text(district, 100, "District");
    Province = Guard.Text(province, 100, "Province");
    Country = Guard.Text(country, 100, "Country") ?? DefaultCountry;
    PostalCode = Guard.Text(postalCode, 20, "Postal code");
    IsPrimary = isPrimary;
  }

  internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary && IsActive;

  internal void Deactivate()
  {
    IsActive = false;
    IsPrimary = false;
  }
}
