using System.Globalization;

/// One surveyed polygon of the property (original, revised or regularized), with the ground slope of
/// the plot in degrees. Optional (req. §25).
///
/// A re-survey is a new boundary row. "Current" means the latest dated survey: a survey older than the one
/// in force is stored as history and does not supersede it; one of the same date or later does (an undated
/// survey cannot be compared, so it supersedes). Points are ordered by sequence_no to draw the shape on a map.
///
/// Slope: 0 to 90 degrees. Above 45 degrees the Building & Construction Regulations 2026 do not allow
/// construction; the survey is still recorded (it is a fact on the ground) and the clerk gets a warning.
public class PropertyBoundary : Aggregate<BoundaryId>
{
  /// numeric(6,2), 0 to 90 inclusive.
  public const decimal MaxSlopeDegrees = 90m;
  /// BCR 2026: steeper than this needs checking before construction is approved.
  public const decimal SteepSlopeDegrees = 45m;
  public const string SlopeRangeMessage = "Slope must be between 0 and 90 degrees.";
  public const int SurveySourceMaxLength = 150;
  public const int RemarksMaxLength = 4000;

  private readonly List<BoundaryPoint> _points = new();

  public PropertyId PropertyId { get; private set; } = default!;
  public BoundaryType BoundaryType { get; private set; }
  public DateOnly? SurveyDate { get; private set; }
  public string? SurveySource { get; private set; }
  /// Ground slope of the surveyed plot in degrees (0 = flat, 90 = vertical).
  public decimal? SlopeDegrees { get; private set; }
  public bool IsCurrent { get; private set; }
  public string? Remarks { get; private set; }

  /// Above the 45° limit of the Building & Construction Regulations 2026.
  public bool IsSteep => SlopeDegrees > SteepSlopeDegrees;

  public IReadOnlyList<BoundaryPoint> Points => _points.OrderBy(p => p.SequenceNo).ToList().AsReadOnly();

  /// Records a survey. currentBoundaries are the property's rows still marked current: they are superseded
  /// when this survey is the latest; otherwise this one is kept as history. Warnings: a steep slope, or a
  /// survey kept as history.
  public static PropertyBoundary Record(
      BoundaryId id,
      Property property,
      BoundaryType boundaryType,
      DateOnly? surveyDate,
      string? surveySource,
      decimal? slopeDegrees,
      string? remarks,
      IReadOnlyCollection<GeoPoint> points,
      DateOnly today,
      IReadOnlyCollection<PropertyBoundary> currentBoundaries,
      out IReadOnlyList<string> warnings)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(currentBoundaries);
    property.EnsureActive();

    if (!Enum.IsDefined(boundaryType))
      throw new DomainException("Unknown boundary type.");

    if (slopeDegrees is < 0 or > MaxSlopeDegrees)
      throw new DomainException(SlopeRangeMessage);

    if (surveyDate > today)
      throw new DomainException("The survey date cannot be in the future.");

    var boundary = new PropertyBoundary
    {
      Id = id,
      PropertyId = property.Id,
      BoundaryType = boundaryType,
      SurveyDate = surveyDate,
      SurveySource = Guard.Text(surveySource, SurveySourceMaxLength, "Survey source"),
      SlopeDegrees = slopeDegrees is null ? null : decimal.Round(slopeDegrees.Value, 2),
      IsCurrent = true,
      Remarks = Guard.Text(remarks, RemarksMaxLength, "Remarks")
    };

    var sequence = 0;
    foreach (var point in GeoPoint.Polygon(points))
      boundary._points.Add(BoundaryPoint.Create(BoundaryPointId.New(), id, ++sequence, point));

    var notes = new List<string>();

    if (boundary.IsSteep)
      notes.Add(SteepSlopeWarning(boundary.SlopeDegrees!.Value));

    var current = currentBoundaries.Where(b => b.IsCurrent && b.PropertyId == property.Id).ToList();
    var newer = surveyDate is null ? null : current.Where(b => b.SurveyDate > surveyDate).MaxBy(b => b.SurveyDate);

    if (newer is not null)
    {
      boundary.IsCurrent = false;
      notes.Add($"Surveyed on {surveyDate:yyyy-MM-dd}, before the current boundary survey of {newer.SurveyDate:yyyy-MM-dd}: it is kept as history and the current boundary stays in force.");
    }
    else
    {
      current.ForEach(b => b.Supersede());
    }

    warnings = notes;
    return boundary;
  }

  public static string SteepSlopeWarning(decimal slopeDegrees) =>
      $"Slope {slopeDegrees.ToString("0.##", CultureInfo.InvariantCulture)}° is above the 45° limit of the Building & Construction Regulations 2026 - the survey is recorded; check before approving construction.";

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
