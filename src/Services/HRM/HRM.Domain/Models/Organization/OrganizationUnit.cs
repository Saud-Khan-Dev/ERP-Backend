/// What an org unit is during one period: its name, type, parent, location and head post.
public sealed record OrganizationUnitDetails(
  string Name,
  OrganizationUnitTypeId UnitTypeId,
  OrganizationUnitId? ParentUnitId,
  LocationId? LocationId,
  PostId? HeadPostId);

/// Immutable identity of an org unit: the code never changes. Name, type, parent, location and head post live in
/// effective-dated versions, so a restructuring never destroys history (the same pattern as Post / PostVersion).
/// The versions cover one continuous timeline: each new version closes the one before it the day before it starts.
public class OrganizationUnit : Aggregate<OrganizationUnitId>
{
  public const int CodeMaxLength = 30;

  private readonly List<OrganizationUnitVersion> _versions = new();

  public string Code { get; private set; } = default!;
  public IReadOnlyList<OrganizationUnitVersion> Versions => _versions.AsReadOnly();

  /// The version that started last (open-ended unless the unit was closed by a later restructuring).
  public OrganizationUnitVersion Latest => _versions.OrderByDescending(v => v.EffectiveFrom).First();

  public static OrganizationUnit Create(OrganizationUnitId id, string code, OrganizationUnitDetails details, DateOnly effectiveFrom)
  {
    var unit = new OrganizationUnit { Id = id, Code = Guard.Code(code, CodeMaxLength, "Unit code") };
    unit.EnsureNotOwnParent(details);
    unit._versions.Add(OrganizationUnitVersion.Open(unit.Id, details, effectiveFrom, RecordStatus.Active));
    return unit;
  }

  /// The version in effect on a date, or null when the unit did not exist yet.
  public OrganizationUnitVersion? VersionOn(DateOnly date) =>
      _versions.FirstOrDefault(v => v.Range.Contains(date));

  public bool IsActiveOn(DateOnly date) => VersionOn(date)?.Status == RecordStatus.Active;

  /// A restructuring from a date: rename, move under another parent, change type, location or head post.
  public OrganizationUnitVersion Restructure(OrganizationUnitDetails details, DateOnly effectiveFrom) =>
      Append(details, effectiveFrom, RecordStatus.Active);

  /// The unit stops existing from a date. Its history stays; it no longer appears in the current structure.
  public OrganizationUnitVersion Deactivate(DateOnly effectiveFrom)
  {
    if (Latest.Status == RecordStatus.Inactive)
      throw new DomainException($"Unit {Code} is already inactive.");

    return Append(Latest.Details, effectiveFrom, RecordStatus.Inactive);
  }

  public OrganizationUnitVersion Reactivate(DateOnly effectiveFrom)
  {
    if (Latest.Status == RecordStatus.Active)
      throw new DomainException($"Unit {Code} is already active.");

    return Append(Latest.Details, effectiveFrom, RecordStatus.Active);
  }

  /// Fixes a mistake in the latest version without starting a new period (a typo in the name, a wrong location).
  public void CorrectLatest(OrganizationUnitDetails details)
  {
    EnsureNotOwnParent(details);
    Latest.Correct(details);
  }

  private OrganizationUnitVersion Append(OrganizationUnitDetails details, DateOnly effectiveFrom, RecordStatus status)
  {
    EnsureNotOwnParent(details);

    var latest = Latest;
    if (effectiveFrom <= latest.EffectiveFrom)
      throw new DomainException($"A new version of unit {Code} must start after {latest.EffectiveFrom:yyyy-MM-dd}, when the current one started.");

    if (latest.EffectiveTo is { } end && effectiveFrom <= end)
      throw new DomainException($"Unit {Code} already has a version until {end:yyyy-MM-dd}.");

    latest.CloseOn(effectiveFrom.AddDays(-1));

    var version = OrganizationUnitVersion.Open(Id, details, effectiveFrom, status);
    _versions.Add(version);
    return version;
  }

  private void EnsureNotOwnParent(OrganizationUnitDetails details)
  {
    if (details.ParentUnitId == Id)
      throw new DomainException("A unit cannot be its own parent.");
  }
}

/// One effective-dated version of an org unit (table organization_unit_version).
public class OrganizationUnitVersion : Entity<OrganizationUnitVersionId>
{
  public const int NameMaxLength = 200;

  public OrganizationUnitId OrgUnitId { get; private set; } = default!;
  public OrganizationUnitTypeId UnitTypeId { get; private set; } = default!;
  public OrganizationUnitId? ParentUnitId { get; private set; }
  public string Name { get; private set; } = default!;
  public LocationId? LocationId { get; private set; }
  public PostId? HeadPostId { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public RecordStatus Status { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public OrganizationUnitDetails Details => new(Name, UnitTypeId, ParentUnitId, LocationId, HeadPostId);

  internal static OrganizationUnitVersion Open(OrganizationUnitId unitId, OrganizationUnitDetails details, DateOnly effectiveFrom, RecordStatus status)
  {
    var version = new OrganizationUnitVersion
    {
      Id = OrganizationUnitVersionId.New(),
      OrgUnitId = unitId,
      EffectiveFrom = effectiveFrom,
      Status = status
    };
    version.Apply(details);
    return version;
  }

  internal void Correct(OrganizationUnitDetails details) => Apply(details);

  internal void CloseOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  private void Apply(OrganizationUnitDetails details)
  {
    ArgumentNullException.ThrowIfNull(details.UnitTypeId);

    Name = Guard.RequiredText(details.Name, NameMaxLength, "Unit name");
    UnitTypeId = details.UnitTypeId;
    ParentUnitId = details.ParentUnitId;
    LocationId = details.LocationId;
    HeadPostId = details.HeadPostId;
  }
}
