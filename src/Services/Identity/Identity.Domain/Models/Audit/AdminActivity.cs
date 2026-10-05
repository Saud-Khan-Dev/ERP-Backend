/// One line in the administration activity trail: who did what, to whom, and from where.
///
/// Append-only. The actor and the target are stored by name as well as by id, so the line still
/// reads after an account or a role is renamed or removed. It records only changes - who looked at
/// a screen is not an administrative action and is not kept here.
public class AdminActivity : Entity<ActivityId>
{
  public DateTime OccurredAt { get; private set; }

  public UserId ActorUserId { get; private set; } = default!;
  /// The actor's username, snapshotted so the line reads even after a rename or deletion.
  public string ActorName { get; private set; } = default!;

  public ActivityAction Action { get; private set; }

  public ActivityTargetType TargetType { get; private set; }
  /// Null for a system-wide change (e.g. the security settings).
  public Guid? TargetId { get; private set; }
  /// The target's name at the time (a username, a role name, or a short label).
  public string? TargetLabel { get; private set; }

  /// Specifics worth keeping, already human-readable (e.g. "Estate officer", "PROPERTY.APPROVE · Deny").
  public string? Detail { get; private set; }

  public IpAddress? IpAddress { get; private set; }
  public string? UserAgent { get; private set; }

  public static AdminActivity Record(
      UserId actorUserId,
      string actorName,
      ActivityAction action,
      ActivityTargetType targetType,
      Guid? targetId,
      string? targetLabel,
      string? detail,
      IpAddress? ip,
      string? userAgent,
      DateTime now) =>
    new()
    {
      Id = ActivityId.Of(Guid.NewGuid()),
      ActorUserId = actorUserId,
      ActorName = Clip(actorName, 150) ?? "unknown",
      Action = action,
      TargetType = targetType,
      TargetId = targetId,
      TargetLabel = Clip(targetLabel, 200),
      Detail = Clip(detail, 500),
      IpAddress = ip,
      UserAgent = Clip(userAgent, 512),
      OccurredAt = now
    };

  private static string? Clip(string? value, int max)
  {
    if (string.IsNullOrWhiteSpace(value)) return null;
    value = value.Trim();
    return value.Length > max ? value[..max] : value;
  }
}
