/// Writes one line to the administration activity trail. The line is added to the current unit of
/// work, so it is saved in the same transaction as the change it records - if the change is rolled
/// back, nothing is logged.
public interface IActivityRecorder
{
  Task RecordAsync(
      ActivityAction action,
      ActivityTargetType targetType,
      Guid? targetId,
      string? targetLabel,
      string? detail,
      CancellationToken cancellationToken);

  /// A line whose actor is not the caller: the person themself when the request carries no token (sign-out, a stolen
  /// refresh token noticed), or the system (`actorId` null, `systemActor` "Startup", "Daily check").
  Task RecordAsAsync(
      UserId? actorId,
      string? systemActor,
      ActivityAction action,
      ActivityTargetType targetType,
      Guid? targetId,
      string? targetLabel,
      string? detail,
      CancellationToken cancellationToken);
}
