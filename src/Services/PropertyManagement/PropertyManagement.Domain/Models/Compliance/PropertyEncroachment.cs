/// One encroachment case; a property can have several over the years (req. §26).
///
/// The encroached area is fixed once recorded (a different area is a new case); the case moves through
/// notice and resolution on its row. Its polygon is separate from the property's boundary points so the
/// map can draw both (req. §27). A regularized encroachment may also open an area regularization case.
public class PropertyEncroachment : Aggregate<EncroachmentId>
{
  private readonly List<EncroachmentBoundaryPoint> _points = new();

  public PropertyId PropertyId { get; private set; } = default!;
  /// ENC-00001, issued by the ENCROACHMENT code sequence.
  public BusinessCode EncroachmentNo { get; private set; } = default!;
  public decimal EncroachmentArea { get; private set; }
  public MasterId MeasurementUnitId { get; private set; } = default!;
  public decimal EncroachmentAreaBase { get; private set; }
  public MasterId EncroachmentStatusId { get; private set; } = default!;
  public string? EncroacherName { get; private set; }
  public OwnerId? EncroacherOwnerId { get; private set; }
  public DateOnly DetectionDate { get; private set; }
  public DateOnly? EffectiveDate { get; private set; }
  public string? NoticeNo { get; private set; }
  public DateOnly? NoticeDate { get; private set; }
  public DateOnly? ResolutionDate { get; private set; }
  public EncroachmentResolution? ResolutionType { get; private set; }
  public string? ResolutionReferenceNo { get; private set; }
  public string? Description { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<EncroachmentBoundaryPoint> Points => _points.OrderBy(p => p.SequenceNo).ToList().AsReadOnly();

  /// Still counts toward the property's encroached area.
  public bool IsUnresolved => ResolutionDate is null;

  public sealed record Details(
      string? EncroacherName,
      PropertyOwner? Encroacher,
      DateOnly? EffectiveDate,
      string? Description,
      string? Remarks);

  public static PropertyEncroachment Record(
      EncroachmentId id,
      Property property,
      BusinessCode encroachmentNo,
      decimal encroachmentArea,
      MeasurementUnit unit,
      EncroachmentStatus status,
      DateOnly detectionDate,
      Details details,
      IReadOnlyCollection<GeoPoint>? points)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(encroachmentNo);
    ArgumentNullException.ThrowIfNull(unit);
    ArgumentNullException.ThrowIfNull(status);
    property.EnsureActive();
    unit.EnsureActive();
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Resolved) || status.Is(SystemMasterCodes.Regularized))
      throw new DomainException("A new encroachment cannot start resolved; record it and then resolve it.");

    var encroachment = new PropertyEncroachment
    {
      Id = id,
      PropertyId = property.Id,
      EncroachmentNo = encroachmentNo,
      EncroachmentArea = decimal.Round(Guard.Positive(encroachmentArea, "Encroachment area"), 4),
      MeasurementUnitId = unit.Id,
      EncroachmentAreaBase = unit.ToBase(encroachmentArea),
      EncroachmentStatusId = status.Id,
      DetectionDate = detectionDate
    };

    encroachment.Apply(details);

    if (points is { Count: > 0 })
      encroachment.SetBoundary(points);

    return encroachment;
  }

  public void Update(Details details)
  {
    EnsureUnresolved();
    Apply(details);
  }

  public void IssueNotice(string noticeNo, DateOnly noticeDate)
  {
    EnsureUnresolved();

    if (noticeDate < DetectionDate)
      throw new DomainException("A notice cannot be issued before the encroachment was detected.");

    NoticeNo = Guard.RequiredText(noticeNo, 50, "Notice no.");
    NoticeDate = noticeDate;
  }

  /// Under Notice, Under Litigation ... Resolving has its own step.
  public void ChangeStatus(EncroachmentStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureUnresolved();
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Resolved) || status.Is(SystemMasterCodes.Regularized))
      throw new DomainException("Use resolve, so the resolution date and type are recorded.");

    EncroachmentStatusId = status.Id;
  }

  /// Removed / litigated / other → RESOLVED; regularized → REGULARIZED.
  public void Resolve(EncroachmentStatus resolvedStatus, DateOnly resolutionDate, EncroachmentResolution resolutionType, string? referenceNo)
  {
    ArgumentNullException.ThrowIfNull(resolvedStatus);
    EnsureUnresolved();

    if (!Enum.IsDefined(resolutionType))
      throw new DomainException("Unknown resolution type.");

    resolvedStatus.EnsureIs(resolutionType == EncroachmentResolution.Regularized ? SystemMasterCodes.Regularized : SystemMasterCodes.Resolved);

    if (resolutionDate < DetectionDate)
      throw new DomainException("An encroachment cannot be resolved before it was detected.");

    EncroachmentStatusId = resolvedStatus.Id;
    ResolutionDate = resolutionDate;
    ResolutionType = resolutionType;
    ResolutionReferenceNo = Guard.Text(referenceNo, 100, "Resolution reference no.");
  }

  /// The encroached polygon can be captured once; it is never overwritten.
  public void SetBoundary(IReadOnlyCollection<GeoPoint> points)
  {
    if (_points.Count > 0)
      throw new DomainException($"Encroachment {EncroachmentNo.Value} already has its boundary.");

    var sequence = 0;
    foreach (var point in GeoPoint.Polygon(points))
      _points.Add(EncroachmentBoundaryPoint.Create(EncroachmentPointId.New(), Id, ++sequence, point));
  }

  private void EnsureUnresolved()
  {
    if (!IsUnresolved)
      throw new DomainException($"Encroachment {EncroachmentNo.Value} is resolved and can no longer change.");
  }

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);
    details.Encroacher?.EnsureActive();

    Guard.DateOrder(DetectionDate, details.EffectiveDate, "Detection date", "Effective date");

    EncroacherName = Guard.Text(details.EncroacherName, 200, "Encroacher name") ?? details.Encroacher?.OwnerName.Value;
    EncroacherOwnerId = details.Encroacher?.Id;
    EffectiveDate = details.EffectiveDate;
    Description = Guard.Text(details.Description, 4000, "Description");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }
}

/// One GPS corner of the encroached area — its own polygon, never shared with the property boundary.
public class EncroachmentBoundaryPoint : Entity<EncroachmentPointId>
{
  public EncroachmentId EncroachmentId { get; private set; } = default!;
  public int SequenceNo { get; private set; }
  public decimal Latitude { get; private set; }
  public decimal Longitude { get; private set; }

  internal static EncroachmentBoundaryPoint Create(EncroachmentPointId id, EncroachmentId encroachmentId, int sequenceNo, GeoPoint point) => new()
  {
    Id = id,
    EncroachmentId = encroachmentId,
    SequenceNo = sequenceNo,
    Latitude = point.Latitude,
    Longitude = point.Longitude
  };
}
