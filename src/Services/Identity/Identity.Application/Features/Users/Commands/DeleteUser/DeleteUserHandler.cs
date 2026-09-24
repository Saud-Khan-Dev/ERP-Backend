using Microsoft.EntityFrameworkCore;

public class DeleteUserHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser)
  : ICommandHandler<DeleteUserCommand, Result<DeleteUserCommandResult>>
{
  public async Task<Result<DeleteUserCommandResult>> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = UserId.Of(command.Id);

    guard.EnsureNotSelf(id, "delete your own account");
    await guard.EnsureNotLastSuperAdminAsync(id, "deleting it", cancellationToken);

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    user.SoftDelete(currentUser.AuditName, now);

    var sessions = await context.Sessions
        .Where(s => s.UserId == id && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, "Account deleted");

    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteUserCommandResult>.Success(new DeleteUserCommandResult(true));
  }
}
