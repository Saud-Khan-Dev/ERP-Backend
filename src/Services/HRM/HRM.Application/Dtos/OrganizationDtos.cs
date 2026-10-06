public sealed record OrgUnitVersionDto(
  Guid Id,
  Guid UnitTypeId,
  string? UnitType,
  Guid? ParentUnitId,
  string? ParentName,
  string Name,
  Guid? LocationId,
  string? Location,
  Guid? HeadPostId,
  string? HeadPostCode,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  RecordStatus Status);

public sealed record OrgUnitDto(Guid Id, string Code, OrgUnitVersionDto? Current, IReadOnlyList<OrgUnitVersionDto> Versions, DateTime? CreatedAt);

/// A unit as it is on the requested date, with its place in the tree and its posts' seats on that date.
public sealed record OrgUnitListItemDto(
  Guid Id,
  string Code,
  string Name,
  Guid UnitTypeId,
  string? UnitType,
  Guid? ParentUnitId,
  string? ParentName,
  Guid? LocationId,
  string? Location,
  Guid? HeadPostId,
  string? HeadPostCode,
  RecordStatus Status,
  DateOnly EffectiveFrom,
  int Depth,
  int SanctionedSeats,
  int FilledSeats);

public sealed record OrgUnitNodeDto(
  Guid Id,
  string Code,
  string Name,
  string? UnitType,
  Guid? HeadPostId,
  string? HeadPostCode,
  int SanctionedSeats,
  int FilledSeats,
  IReadOnlyList<OrgUnitNodeDto> Children);

public sealed record PayScaleStageDto(Guid Id, int StageNumber, decimal BasicPay);

public sealed record PayScaleVersionDto(
  Guid Id,
  Guid GradeId,
  int Bps,
  decimal MinBasicPay,
  decimal MaxBasicPay,
  string? IncrementRule,
  string? VersionLabel,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  RecordStatus Status,
  Guid? ApprovedBy,
  bool InUse,
  IReadOnlyList<PayScaleStageDto> Stages);

public sealed record PostVersionDto(
  Guid Id,
  Guid DesignationId,
  string? Designation,
  Guid GradeId,
  int Bps,
  Guid OrgUnitId,
  string? OrgUnit,
  Guid? ReportingPostId,
  string? ReportingPostCode,
  EmploymentType EmploymentType,
  Guid? LocationId,
  string? Location,
  int SanctionedCount,
  PostLifecycle LifecycleStatus,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  Guid? ApprovedBy);

public sealed record PostHolderDto(Guid AssignmentId, Guid EmployeeId, string EmployeeNumber, string FullName, AssignmentType AssignmentType, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record PostListItemDto(
  Guid Id,
  string PostCode,
  Guid DesignationId,
  string? Designation,
  Guid GradeId,
  int Bps,
  Guid OrgUnitId,
  string? OrgUnit,
  EmploymentType EmploymentType,
  int SanctionedCount,
  int FilledCount,
  PositionStatus Status,
  PostLifecycle LifecycleStatus,
  DateOnly EffectiveFrom,
  IReadOnlyList<PostHolderDto> Holders);

public sealed record PostDto(
  Guid Id,
  string PostCode,
  PostVersionDto? Current,
  IReadOnlyList<PostVersionDto> Versions,
  IReadOnlyList<PostHolderDto> Assignments,
  DateTime? CreatedAt);

public static class OrganizationMappings
{
  /// The derived occupancy, as v_post_occupancy works it out.
  public static PositionStatus OccupancyStatus(PostLifecycle lifecycle, int sanctioned, int filled) => lifecycle switch
  {
    PostLifecycle.Abolished => PositionStatus.Abolished,
    PostLifecycle.Frozen => PositionStatus.Frozen,
    _ when filled == 0 => PositionStatus.Vacant,
    _ when filled < sanctioned => PositionStatus.PartiallyFilled,
    _ => PositionStatus.Filled
  };

  public static PayScaleVersionDto ToDto(this PayScaleVersion x, int bps, bool inUse) => new(
    x.Id.Value, x.GradeId.Value, bps, x.MinBasicPay, x.MaxBasicPay, x.IncrementRule, x.VersionLabel, x.NotificationRef,
    x.EffectiveFrom, x.EffectiveTo, x.Status, x.ApprovedBy, inUse,
    x.Stages.Select(s => new PayScaleStageDto(s.Id.Value, s.StageNumber, s.BasicPay)).ToList());
}
