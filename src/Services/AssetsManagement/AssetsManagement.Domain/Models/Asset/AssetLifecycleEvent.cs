using System.Text.Json;

public class AssetLifecycleEvent : Aggregate<AssetLifecycleEventId>
{
  public AssetId AssetId { get; private set; } = default!;
  public LifecycleEventTypeId EventTypeId { get; private set; } = default!;
  public DateTime EventDate { get; private set; }
  public AssetStatusId? FromStatusId { get; private set; }
  public AssetStatusId? ToStatusId { get; private set; }
  /// Who recorded it (the signed-in user) and the name shown for them.
  public Guid? PerformedBy { get; private set; }
  public string? PerformedByName { get; private set; }
  public string? Notes { get; private set; }
  /// Event-specific payload: what changed, the vendor, the cost …
  public JsonElement? Details { get; private set; }

  public static AssetLifecycleEvent Create(
      AssetLifecycleEventId id,
      AssetId assetId,
      LifecycleEventType eventType,
      DateTime eventDate,
      AssetStatusId? fromStatusId,
      AssetStatusId? toStatusId,
      ActorRef performedBy,
      string? notes,
      JsonElement? details)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentNullException.ThrowIfNull(eventType);
    ArgumentNullException.ThrowIfNull(performedBy);

    if (!eventType.IsActive)
      throw new DomainException($"The event type '{eventType.Name.Value}' is switched off.");

    return new AssetLifecycleEvent
    {
      Id = id,
      AssetId = assetId,
      EventTypeId = eventType.Id,
      EventDate = eventDate,
      FromStatusId = fromStatusId,
      ToStatusId = toStatusId,
      PerformedBy = performedBy.Id,
      PerformedByName = performedBy.ShortName,
      Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
      Details = details is { ValueKind: JsonValueKind.Object } d && !d.EnumerateObject().Any() ? null : details
    };
  }
}
