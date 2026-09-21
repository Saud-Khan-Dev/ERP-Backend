/// SITE / BUILDING / FLOOR / ROOM tree. Path mirrors asset_category.path for fast subtree queries.
public class Location : Aggregate<LocationId>
{
  public const char PathSeparator = '.';

  public LocationId? ParentLocationId { get; private set; }
  public LookupCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? LocationType { get; private set; }
  public string? Path { get; private set; }
  public string? Address { get; private set; }
  public decimal? Latitude { get; private set; }
  public decimal? Longitude { get; private set; }
  public bool IsActive { get; private set; }

  public static Location Create(
      LocationId id,
      Location? parent,
      LookupCode code,
      Name name,
      string? locationType,
      string? address,
      decimal? latitude,
      decimal? longitude)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    ValidateCoordinates(latitude, longitude);

    var location = new Location
    {
      Id = id,
      Code = code,
      Name = name,
      LocationType = NormalizeType(locationType),
      Address = address,
      Latitude = latitude,
      Longitude = longitude,
      IsActive = true
    };

    location.Rebase(parent);
    return location;
  }

  public void Update(LookupCode code, Name name, string? locationType, string? address, decimal? latitude, decimal? longitude, bool isActive)
  {
    ArgumentNullException.ThrowIfNull(code);
    ArgumentNullException.ThrowIfNull(name);
    ValidateCoordinates(latitude, longitude);

    Code = code;
    Name = name;
    LocationType = NormalizeType(locationType);
    Address = address;
    Latitude = latitude;
    Longitude = longitude;
    IsActive = isActive;
  }

  /// Re-parents the node and recomputes its path. Descendants must be rebased afterwards (root -> leaf order).
  public void Rebase(Location? parent)
  {
    if (parent is not null)
    {
      if (parent.Id == Id)
        throw new DomainException("A location cannot be its own parent.");

      if (Path is not null && IsAncestorOf(parent))
        throw new DomainException("Moving a location under one of its own descendants would create a cycle.");
    }

    ParentLocationId = parent?.Id;
    Path = parent is null
        ? Code.ToPathLabel()
        : string.Concat(parent.Path, PathSeparator, Code.ToPathLabel());
  }

  public bool IsAncestorOf(Location other) =>
      Path is not null && other.Path is not null && other.Path.StartsWith(Path + PathSeparator, StringComparison.Ordinal);

  private static string? NormalizeType(string? locationType) =>
      string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim().ToUpperInvariant();

  private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
  {
    if (latitude is < -90 or > 90)
      throw new DomainException("Latitude must be between -90 and 90.");

    if (longitude is < -180 or > 180)
      throw new DomainException("Longitude must be between -180 and 180.");
  }
}
