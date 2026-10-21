using System.Text.Json;

/// One line of the organisation-wide lifecycle timeline: the event with the asset and event type it concerns, and
/// who recorded it.
public sealed record LifecycleTimelineItemDto(
  Guid Id,
  Guid AssetId,
  string AssetCode,
  string AssetName,
  Guid EventTypeId,
  string EventTypeCode,
  string EventTypeName,
  string? Stage,
  EventPurpose? Purpose,
  DateTime EventDate,
  Guid? FromStatusId,
  Guid? ToStatusId,
  Guid? PerformedBy,
  string? PerformedByName,
  string? Notes,
  JsonElement? Details);

public sealed record GetLifecycleTimelineQueryResult(PaginatedResult<LifecycleTimelineItemDto> Events);

/// Every asset's lifecycle events (deleted assets left out), newest first. Stage filters by the event type's stage
/// (MAINTENANCE, ASSIGNMENT ...); class, category (with its sub-categories) and location (with the places inside it)
/// filter by the asset.
public sealed record GetLifecycleTimelineQuery(
  PaginationRequest Pagination,
  Guid? AssetId = null,
  Guid? EventTypeId = null,
  string? Stage = null,
  DateTime? From = null,
  DateTime? To = null,
  Guid? AssetClassId = null,
  Guid? CategoryId = null,
  Guid? LocationId = null) : IQuery<Result<GetLifecycleTimelineQueryResult>>;
