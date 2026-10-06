/// A place where GDA works: head office, a town office, a site. Org units, posts and holidays can point at one.
public class Location : Aggregate<LocationId>
{
  public const string DefaultProvince = "Khyber Pakhtunkhwa";

  public string Name { get; private set; } = default!;
  public string? AddressLine { get; private set; }
  public string? City { get; private set; }
  public string? District { get; private set; }
  public string? Province { get; private set; }
  public bool IsActive { get; private set; }

  public static Location Create(LocationId id, string name, string? addressLine, string? city, string? district, string? province)
  {
    var location = new Location { Id = id, IsActive = true };
    location.Update(name, addressLine, city, district, province);
    return location;
  }

  public void Update(string name, string? addressLine, string? city, string? district, string? province)
  {
    Name = Guard.RequiredText(name, 150, "Location name");
    AddressLine = Guard.Text(addressLine, 300, "Address");
    City = Guard.Text(city, 100, "City");
    District = Guard.Text(district, 100, "District");
    Province = Guard.Text(province, 100, "Province") ?? DefaultProvince;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Location '{Name}' is inactive.");
  }
}
