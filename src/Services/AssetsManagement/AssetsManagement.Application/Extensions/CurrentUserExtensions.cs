/// The signed-in user as the records keep them: id and the name shown for them - the display name first, the same
/// name the audit trail and the created / changed columns write (`ICurrentUser.AuditName`).
public static class CurrentUserExtensions
{
  public static ActorRef Actor(this ICurrentUser user) => new(user.UserId, user.AuditName);
}
