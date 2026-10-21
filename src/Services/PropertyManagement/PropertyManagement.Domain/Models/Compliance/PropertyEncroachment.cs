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

  public const int NoticeNoMaxLength = 50;
  public const int EncroacherNameMaxLength = 200;
  public const int ResolutionReferenceMaxLength = 100;
  public const int TextMaxLength = 4000;

  /// EffectiveDate is "in place since": when the encroachment started, on or before its detection.
  public sealed record Details(
      string? EncroacherName,
      PropertyOwner? Encroacher,
      DateOnly? EffectiveDate,
      string? Description,
      string? Remarks);

  /// A notice served when the encroachment is recorded. Without a date it is dated on the detection date.
  public sealed record Notice(string? NoticeNo, DateOnly? NoticeDate);

  /// Records the case. A notice given here moves an ACTIVE case to UNDER_NOTICE (underNoticeStatus).
  public static PropertyEncroachment Record(
      EncroachmentId id,
      Property property,
      BusinessCode encroachmentNo,
      decimal encroachmentArea,
      MeasurementUnit unit,
      EncroachmentStatus status,
      DateOnly detectionDate,
      Details details,
      IReadOnlyCollection<GeoPoint>? points,
      DateOnly today,
      Notice? notice = null,
      EncroachmentStatus? underNoticeStatus = null)
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

    if (detectionDate > today)
      throw new DomainException("The detection date cannot be in the future.");

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

    if (notice is not null && (!string.IsNullOrWhiteSpace(notice.NoticeNo) || notice.NoticeDate is not null))
    {
      if (string.IsNullOrWhiteSpace(notice.NoticeNo))
        throw new DomainException("A notice date needs its notice no. Enter the notice no., or clear the notice date.");

      encroachment.IssueNotice(status, underNoticeStatus ?? throw new ArgumentNullException(nameof(underNoticeStatus)),
        notice.NoticeNo, notice.NoticeDate ?? detectionDate, today);
    }

    return encroachment;
  }

  public void Update(Details details)
  {
    EnsureUnresolved();
    Apply(details);
  }

  /// Serves a notice on the encroacher. An ACTIVE case moves to UNDER_NOTICE; a case already further along
  /// (e.g. UNDER_LITIGATION) keeps its status.
  public void IssueNotice(EncroachmentStatus currentStatus, EncroachmentStatus underNoticeStatus, string noticeNo, DateOnly noticeDate, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    ArgumentNullException.ThrowIfNull(underNoticeStatus);
    EnsureUnresolved();
    EnsureCurrent(currentStatus);
    underNoticeStatus.EnsureIs(SystemMasterCodes.UnderNotice);

    if (noticeDate < DetectionDate)
      throw new DomainException($"A notice cannot be issued before the encroachment was detected ({DetectionDate:yyyy-MM-dd}).");

    if (noticeDate > today)
      throw new DomainException("A notice cannot be dated in the future.");

    NoticeNo = Guard.RequiredText(noticeNo, NoticeNoMaxLength, "Notice no.");
    NoticeDate = noticeDate;

    if (currentStatus.Is(SystemMasterCodes.Active))
      EncroachmentStatusId = underNoticeStatus.Id;
  }

  /// A court case was filed about this encroachment: an unresolved case moves to UNDER_LITIGATION.
  /// A resolved one keeps its status (the court case is about something already settled on the ground).
  public void MarkUnderLitigation(EncroachmentStatus underLitigationStatus)
  {
    ArgumentNullException.ThrowIfNull(underLitigationStatus);
    underLitigationStatus.EnsureIs(SystemMasterCodes.UnderLitigation);

    if (IsUnresolved)
      EncroachmentStatusId = underLitigationStatus.Id;
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

  /// Removed / litigated → RESOLVED; regularized → REGULARIZED. The resolution is a recorded event: not in the future.
  public void Resolve(EncroachmentStatus resolvedStatus, DateOnly resolutionDate, EncroachmentResolution resolutionType, string? referenceNo, DateOnly? today = null)
  {
    ArgumentNullException.ThrowIfNull(resolvedStatus);
    EnsureUnresolved();

    if (today is { } day && resolutionDate > day)
      throw new DomainException($"The resolution date cannot be in the future ({resolutionDate:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    if (!Enum.IsDefined(resolutionType))
      throw new DomainException("Unknown resolution type.");

    resolvedStatus.EnsureIs(resolutionType == EncroachmentResolution.Regularized ? SystemMasterCodes.Regularized : SystemMasterCodes.Resolved);

    if (resolutionDate < DetectionDate)
      throw new DomainException("An encroachment cannot be resolved before it was detected.");

    EncroachmentStatusId = resolvedStatus.Id;
    ResolutionDate = resolutionDate;
    ResolutionType = resolutionType;
    ResolutionReferenceNo = Guard.Text(referenceNo, ResolutionReferenceMaxLength, "Resolution reference no.");
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

  private void EnsureCurrent(EncroachmentStatus currentStatus)
  {
    if (currentStatus.Id != EncroachmentStatusId)
      throw new DomainException("The supplied status does not match the encroachment.");
  }

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);

    // an inactive owner may stay on an old record, but cannot be newly chosen
    if (details.Encroacher is { } encroacher && encroacher.Id != EncroacherOwnerId)
      encroacher.EnsureActive();

    // "in place since" is when the encroachment started: on or before the day it was detected
    if (details.EffectiveDate > DetectionDate)
      throw new DomainException("In place since cannot be after the detection date.");

    EncroacherName = Guard.Text(details.EncroacherName, EncroacherNameMaxLength, "Encroacher name") ?? details.Encroacher?.OwnerName.Value;
    EncroacherOwnerId = details.Encroacher?.Id;
    EffectiveDate = details.EffectiveDate;
    Description = Guard.Text(details.Description, TextMaxLength, "Description");
    Remarks = Guard.Text(details.Remarks, TextMaxLength, "Remarks");
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
