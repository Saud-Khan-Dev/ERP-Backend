/// One GPS corner of a polygon: latitude / longitude only, decimal(10,7). No elevation is stored.
public sealed record GeoPoint
{
  public decimal Latitude { get; }
  public decimal Longitude { get; }

  private GeoPoint(decimal latitude, decimal longitude)
  {
    Latitude = latitude;
    Longitude = longitude;
  }

  public static GeoPoint Of(decimal latitude, decimal longitude)
  {
    if (latitude is < -90 or > 90)
      throw new DomainException("Latitude must be between -90 and 90.");

    if (longitude is < -180 or > 180)
      throw new DomainException("Longitude must be between -180 and 180.");

    return new GeoPoint(decimal.Round(latitude, 7), decimal.Round(longitude, 7));
  }

  /// A polygon needs at least three distinct corners.
  public static IReadOnlyList<GeoPoint> Polygon(IReadOnlyCollection<GeoPoint> points)
  {
    ArgumentNullException.ThrowIfNull(points);

    if (points.Distinct().Count() < 3)
      throw new DomainException("A boundary needs at least three distinct GPS points.");

    return points.ToList();
  }
}
