/// The signed-in user as the records keep them: id and the name shown for them.
public static class CurrentUserExtensions
{
  public static ActorRef Actor(this ICurrentUser user) => new(user.UserId, user.Username ?? user.AuditName);
}
