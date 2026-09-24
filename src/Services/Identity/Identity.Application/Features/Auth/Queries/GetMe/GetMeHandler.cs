using Microsoft.EntityFrameworkCore;

public class GetMeHandler(IApplicationDbContext context, ICurrentUser currentUser, IUserPermissionService permissionService)
  : IQueryHandler<GetMeQuery, Result<GetMeQueryResult>>
{
  public async Task<Result<GetMeQueryResult>> Handle(GetMeQuery query, CancellationToken cancellationToken)
  {
    var userId = currentUser.UserId
      ?? throw new InvalidCredentialsException("No authenticated user");

    var user = await context.Users.AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == UserId.Of(userId), cancellationToken)
      ?? throw new UserNotFoundException($"User {userId} was not found.");

    // resolved live rather than read from the token, so a role change shows up immediately here
    var resolved = await permissionService.ResolveAsync(user.Id, cancellationToken);

    return Result<GetMeQueryResult>.Success(new GetMeQueryResult(user.ToCurrentUserDto(resolved)));
  }
}
