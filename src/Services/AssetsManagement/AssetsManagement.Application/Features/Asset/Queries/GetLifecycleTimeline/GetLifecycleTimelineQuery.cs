using System.Text.Json;

/// One line of the organisation-wide lifecycle timeline: the event with the asset and event type it concerns.
public sealed record LifecycleTimelineItemDto(
  Guid Id,
  Guid AssetId,
  string AssetCode,
  string AssetName,
  Guid EventTypeId,
  string EventTypeCode,
  string EventTypeName,
  string? Stage,
  DateTime EventDate,
  Guid? FromStatusId,
  Guid? ToStatusId,
  Guid? PerformedBy,
  string? Notes,
  JsonElement? Details);

public sealed record GetLifecycleTimelineQueryResult(PaginatedResult<LifecycleTimelineItemDto> Events);

/// Every asset's lifecycle events, newest first. Stage filters by the event type's stage (MAINTENANCE, ASSIGNMENT ...).
public sealed record GetLifecycleTimelineQuery(
  PaginationRequest Pagination,
  Guid? AssetId = null,
  Guid? EventTypeId = null,
  string? Stage = null,
  DateTime? From = null,
  DateTime? To = null) : IQuery<Result<GetLifecycleTimelineQueryResult>>;
