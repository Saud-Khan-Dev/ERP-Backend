/// One surveyed polygon of the property (original, revised or regularized), with the ground slope of
/// the plot as a percentage. Optional (req. §25).
///
/// A re-survey is a new boundary row; the previous one stops being current but is kept. Points are
/// ordered by sequence_no to draw the shape on a map.
public class PropertyBoundary : Aggregate<BoundaryId>
{
  /// decimal(8,4)
  public const decimal MaxSlopePercentage = 9999.9999m;

  private readonly List<BoundaryPoint> _points = new();

  public PropertyId PropertyId { get; private set; } = default!;
  public BoundaryType BoundaryType { get; private set; }
  public DateOnly? SurveyDate { get; private set; }
  public string? SurveySource { get; private set; }
  /// Ground slope of the surveyed plot as a percentage: 12.50 = 12.50% slope (not degrees).
  public decimal? SlopePercentage { get; private set; }
  public bool IsCurrent { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<BoundaryPoint> Points => _points.OrderBy(p => p.SequenceNo).ToList().AsReadOnly();

  public static PropertyBoundary Record(
      BoundaryId id,
      Property property,
      BoundaryType boundaryType,
      DateOnly? surveyDate,
      string? surveySource,
      decimal? slopePercentage,
      string? remarks,
      IReadOnlyCollection<GeoPoint> points)
  {
    ArgumentNullException.ThrowIfNull(property);
    property.EnsureActive();

    if (!Enum.IsDefined(boundaryType))
      throw new DomainException("Unknown boundary type.");

    if (slopePercentage is < 0 or > MaxSlopePercentage)
      throw new DomainException($"Slope must be a percentage between 0 and {MaxSlopePercentage} (e.g. 12.50 for a 12.50% slope).");

    var boundary = new PropertyBoundary
    {
      Id = id,
      PropertyId = property.Id,
      BoundaryType = boundaryType,
      SurveyDate = surveyDate,
      SurveySource = Guard.Text(surveySource, 150, "Survey source"),
      SlopePercentage = slopePercentage is null ? null : decimal.Round(slopePercentage.Value, 4),
      IsCurrent = true,
      Remarks = Guard.Text(remarks, 4000, "Remarks")
    };

    var sequence = 0;
    foreach (var point in GeoPoint.Polygon(points))
      boundary._points.Add(BoundaryPoint.Create(BoundaryPointId.New(), id, ++sequence, point));

    return boundary;
  }

  /// A newer survey replaced this one.
  public void Supersede() => IsCurrent = false;
}

/// One GPS corner of a property boundary: latitude and longitude only.
public class BoundaryPoint : Entity<BoundaryPointId>
{
  public BoundaryId PropertyBoundaryId { get; private set; } = default!;
  public int SequenceNo { get; private set; }
  public decimal Latitude { get; private set; }
  public decimal Longitude { get; private set; }

  internal static BoundaryPoint Create(BoundaryPointId id, BoundaryId boundaryId, int sequenceNo, GeoPoint point) => new()
  {
    Id = id,
    PropertyBoundaryId = boundaryId,
    SequenceNo = sequenceNo,
    Latitude = point.Latitude,
    Longitude = point.Longitude
  };
}
