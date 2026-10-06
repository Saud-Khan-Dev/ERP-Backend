/// What a sanctioned post is during one period.
public sealed record PostDetails(
  DesignationId DesignationId,
  PayScaleGradeId GradeId,
  OrganizationUnitId OrgUnitId,
  PostId? ReportingPostId,
  EmploymentType EmploymentType,
  LocationId? LocationId,
  int SanctionedCount,
  string? NotificationRef);

/// Immutable identity of a sanctioned post (the post code never changes). Designation, grade, unit, reporting line,
/// seats and lifecycle live in effective-dated versions. Whether the seats are filled is derived from the position
/// assignments, never stored, so it cannot drift.
public class Post : Aggregate<PostId>
{
  public const int CodeMaxLength = 30;

  private readonly List<PostVersion> _versions = new();

  public string PostCode { get; private set; } = default!;
  public IReadOnlyList<PostVersion> Versions => _versions.AsReadOnly();

  public PostVersion Latest => _versions.OrderByDescending(v => v.EffectiveFrom).First();

  public static Post Create(PostId id, string postCode, PostDetails details, DateOnly effectiveFrom, Guid? approvedBy)
  {
    var post = new Post { Id = id, PostCode = Guard.Code(postCode, CodeMaxLength, "Post code") };
    post.EnsureNotReportingToItself(details);
    post._versions.Add(PostVersion.Open(post.Id, details, PostLifecycle.Sanctioned, effectiveFrom, approvedBy));
    return post;
  }

  public PostVersion? VersionOn(DateOnly date) => _versions.FirstOrDefault(v => v.Range.Contains(date));

  /// A change from a date: upgrade / downgrade, move to another unit, more or fewer seats, another reporting post.
  /// `filledSeats` is the number of regular holders on the day the change starts.
  public PostVersion Revise(PostDetails details, DateOnly effectiveFrom, int filledSeats, Guid? approvedBy)
  {
    EnsureNotReportingToItself(details);
    EnsureSeatsCover(details.SanctionedCount, filledSeats);
    return Append(details, Latest.LifecycleStatus, effectiveFrom, approvedBy);
  }

  /// No new regular appointment from this date; the current holders stay.
  public PostVersion Freeze(DateOnly effectiveFrom, string? notificationRef, Guid? approvedBy)
  {
    if (Latest.LifecycleStatus != PostLifecycle.Sanctioned)
      throw new DomainException($"Post {PostCode} is {Latest.LifecycleStatus.ToString().ToLowerInvariant()}, not sanctioned, so it cannot be frozen.");

    return Append(Latest.Details with { NotificationRef = notificationRef ?? Latest.NotificationRef }, PostLifecycle.Frozen, effectiveFrom, approvedBy);
  }

  public PostVersion Unfreeze(DateOnly effectiveFrom, string? notificationRef, Guid? approvedBy)
  {
    if (Latest.LifecycleStatus != PostLifecycle.Frozen)
      throw new DomainException($"Post {PostCode} is not frozen.");

    return Append(Latest.Details with { NotificationRef = notificationRef ?? Latest.NotificationRef }, PostLifecycle.Sanctioned, effectiveFrom, approvedBy);
  }

  /// The post ceases to exist from a date. It must have no regular holder on that day.
  public PostVersion Abolish(DateOnly effectiveFrom, string? notificationRef, int filledSeats, Guid? approvedBy)
  {
    if (Latest.LifecycleStatus == PostLifecycle.Abolished)
      throw new DomainException($"Post {PostCode} is already abolished.");

    if (filledSeats > 0)
      throw new DomainException($"Post {PostCode} still has {filledSeats} regular holder(s) on {effectiveFrom:yyyy-MM-dd}. Transfer or relieve them first.");

    return Append(Latest.Details with { NotificationRef = notificationRef ?? Latest.NotificationRef }, PostLifecycle.Abolished, effectiveFrom, approvedBy);
  }

  /// Fixes a mistake in the latest version without starting a new period.
  public void CorrectLatest(PostDetails details, int filledSeats)
  {
    EnsureNotReportingToItself(details);
    EnsureSeatsCover(details.SanctionedCount, filledSeats);
    Latest.Correct(details);
  }

  private PostVersion Append(PostDetails details, PostLifecycle lifecycle, DateOnly effectiveFrom, Guid? approvedBy)
  {
    var latest = Latest;
    if (latest.LifecycleStatus == PostLifecycle.Abolished)
      throw new DomainException($"Post {PostCode} is abolished; create a new post instead.");

    if (effectiveFrom <= latest.EffectiveFrom)
      throw new DomainException($"A new version of post {PostCode} must start after {latest.EffectiveFrom:yyyy-MM-dd}, when the current one started.");

    if (latest.EffectiveTo is { } end && effectiveFrom <= end)
      throw new DomainException($"Post {PostCode} already has a version until {end:yyyy-MM-dd}.");

    latest.CloseOn(effectiveFrom.AddDays(-1));

    var version = PostVersion.Open(Id, details, lifecycle, effectiveFrom, approvedBy);
    _versions.Add(version);
    return version;
  }

  private void EnsureNotReportingToItself(PostDetails details)
  {
    if (details.ReportingPostId == Id)
      throw new DomainException("A post cannot report to itself.");
  }

  private void EnsureSeatsCover(int sanctionedCount, int filledSeats)
  {
    if (sanctionedCount < filledSeats)
      throw new DomainException($"Post {PostCode} has {filledSeats} regular holder(s); it cannot be reduced to {sanctionedCount} seat(s).");
  }
}

/// One version of a post's attributes. sanctioned_count = seats this post carries (1 = a single seat; more = a pool
/// such as "10 Drivers").
public class PostVersion : Entity<PostVersionId>
{
  public PostId PostId { get; private set; } = default!;
  public DesignationId DesignationId { get; private set; } = default!;
  public PayScaleGradeId GradeId { get; private set; } = default!;
  public OrganizationUnitId OrgUnitId { get; private set; } = default!;
  public PostId? ReportingPostId { get; private set; }
  public EmploymentType EmploymentType { get; private set; }
  public LocationId? LocationId { get; private set; }
  public int SanctionedCount { get; private set; }
  public PostLifecycle LifecycleStatus { get; private set; }
  public string? NotificationRef { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public PostDetails Details => new(DesignationId, GradeId, OrgUnitId, ReportingPostId, EmploymentType, LocationId, SanctionedCount, NotificationRef);

  internal static PostVersion Open(PostId postId, PostDetails details, PostLifecycle lifecycle, DateOnly effectiveFrom, Guid? approvedBy)
  {
    var version = new PostVersion
    {
      Id = PostVersionId.New(),
      PostId = postId,
      LifecycleStatus = lifecycle,
      EffectiveFrom = effectiveFrom,
      ApprovedBy = approvedBy
    };
    version.Apply(details);
    return version;
  }

  internal void Correct(PostDetails details) => Apply(details);

  internal void CloseOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  /// A regular appointment needs a sanctioned post (frozen and abolished posts take none).
  public void EnsureAcceptsRegularAppointment(string postCode)
  {
    if (LifecycleStatus != PostLifecycle.Sanctioned)
      throw new DomainException($"Post {postCode} is {LifecycleStatus.ToString().ToLowerInvariant()} and cannot receive a regular assignment.");
  }

  private void Apply(PostDetails details)
  {
    ArgumentNullException.ThrowIfNull(details.DesignationId);
    ArgumentNullException.ThrowIfNull(details.GradeId);
    ArgumentNullException.ThrowIfNull(details.OrgUnitId);

    DesignationId = details.DesignationId;
    GradeId = details.GradeId;
    OrgUnitId = details.OrgUnitId;
    ReportingPostId = details.ReportingPostId;
    EmploymentType = details.EmploymentType;
    LocationId = details.LocationId;
    SanctionedCount = Guard.Positive(details.SanctionedCount, "Sanctioned seats");
    NotificationRef = Guard.Text(details.NotificationRef, 200, "Notification");
  }
}
