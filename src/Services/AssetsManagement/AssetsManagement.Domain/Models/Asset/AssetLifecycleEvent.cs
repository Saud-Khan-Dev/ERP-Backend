using System.Text.Json;

public class AssetLifecycleEvent : Aggregate<AssetLifecycleEventId>
{
  public AssetId AssetId { get; private set; } = default!;
  public LifecycleEventTypeId EventTypeId { get; private set; } = default!;
  public DateTime EventDate { get; private set; }
  public AssetStatusId? FromStatusId { get; private set; }
  public AssetStatusId? ToStatusId { get; private set; }
  public Guid? PerformedBy { get; private set; }
  public string? Notes { get; private set; }
  /// Event-specific payload
  public JsonElement? Details { get; private set; }

  public static AssetLifecycleEvent Create(
      AssetLifecycleEventId id,
      AssetId assetId,
      LifecycleEventType eventType,
      DateTime eventDate,
      AssetStatusId? fromStatusId,
      AssetStatusId? toStatusId,
      Guid? performedBy,
      string? notes,
      JsonElement? details)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(eventType);

    if (!eventType.IsActive)
      throw new DomainException($"Lifecycle event type '{eventType.Name.Value}' is inactive.");

    return new AssetLifecycleEvent
    {
      Id = id,
      AssetId = assetId,
      EventTypeId = eventType.Id,
      EventDate = eventDate,
      FromStatusId = fromStatusId,
      ToStatusId = toStatusId,
      PerformedBy = performedBy,
      Notes = notes,
      Details = details
    };
  }
}
