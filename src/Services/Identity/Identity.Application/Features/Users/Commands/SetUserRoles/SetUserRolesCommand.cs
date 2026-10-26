using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One role in "Change roles": the role, and for a newly given one, the day it ends (temporary cover).
public sealed record RoleGrantInput(Guid RoleId, DateTime? ExpiresAt = null);

/// "Change someone's job" in one act: the exact set of roles the person should hold. Roles they keep are untouched,
/// new ones are given, missing ones taken away - with the same guard rails as giving and taking one role, and one line
/// in the activity trail.
/// `ConflictReason`: why the person may hold two duties that should be kept apart - needed only when the new roles put
/// such a pair together; recorded in the activity trail.
public sealed record SetUserRolesCommand(Guid UserId, IReadOnlyList<RoleGrantInput> Roles, string? ConflictReason = null) : ICommand<Result<SetUserRolesCommandResult>>;

public sealed record SetUserRolesCommandResult(int Added, int Removed);

public class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
  public SetUserRolesCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.Roles).NotNull();
    RuleForEach(x => x.Roles).Must(r => r.RoleId != Guid.Empty).WithMessage("Choose the roles from the list.");
    RuleFor(x => x.Roles).Must(r => r is null || r.Select(x => x.RoleId).Distinct().Count() == r.Count)
      .WithMessage("A role is listed twice.");
  }
}

public class SetUserRolesHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser, IActivityRecorder activity)
  : ICommandHandler<SetUserRolesCommand, Result<SetUserRolesCommandResult>>
{
  public async Task<Result<SetUserRolesCommandResult>> Handle(SetUserRolesCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(command.UserId);

    guard.EnsureNotSelf(userId, "change the roles of");
    var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");
    await guard.EnsureCanActOnAsync(userId, "change the roles of", cancellationToken);

    var wanted = command.Roles.ToDictionary(r => RoleId.Of(r.RoleId), r => r.ExpiresAt);
    var grants = await context.UserRoles.Where(ur => ur.UserId == userId && ur.RevokedAt == null).ToListAsync(cancellationToken);
    var live = grants.Where(g => g.IsLive(now)).ToList();

    var addIds = wanted.Keys.Where(id => live.All(g => g.RoleId != id)).ToList();
    var removeGrants = live.Where(g => !wanted.ContainsKey(g.RoleId)).ToList();
    // a kept role whose end date changed is given again with the new date (one grant per role at a time)
    var redated = live.Where(g => wanted.TryGetValue(g.RoleId, out var end) && end != g.ExpiresAt).ToList();
    if (addIds.Count == 0 && removeGrants.Count == 0 && redated.Count == 0)
      return Result<SetUserRolesCommandResult>.Success(new SetUserRolesCommandResult(0, 0));

    var touchedIds = addIds.Concat(removeGrants.Select(g => g.RoleId)).Concat(redated.Select(g => g.RoleId)).Distinct().ToList();
    var roles = await context.Roles.Where(r => touchedIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
    if (addIds.Any(id => !roles.ContainsKey(id)))
      throw new RoleNotFoundException("One of the chosen roles was not found - it may have been deleted.");

    foreach (var role in roles.Values)
      guard.EnsureCanAdministerRole(role);
    var switchedOff = addIds.Select(id => roles[id]).FirstOrDefault(r => !r.IsActive);
    if (switchedOff is not null)
      return Result<SetUserRolesCommandResult>.Failure($"The role {switchedOff.RoleName.Value} is switched off and cannot be given.");
    await guard.EnsureCanGrantRolesAsync(addIds, "give these roles", cancellationToken);
    if (removeGrants.Any(g => roles.TryGetValue(g.RoleId, out var r) && r.IsSuperAdmin))
      await guard.EnsureNotLastSuperAdminAsync(userId, "taking this role away", cancellationToken);

    foreach (var id in addIds)
    {
      // a grant of the same role that ran out is ended first (one grant per role at a time)
      foreach (var expired in grants.Where(g => g.RoleId == id && !g.IsLive(now)))
        expired.Revoke(now);
      await context.UserRoles.AddAsync(UserRole.Create(userId, id, currentUser.UserId, wanted[id], now), cancellationToken);
    }
    foreach (var grant in removeGrants)
      grant.Revoke(now);
    foreach (var grant in redated)
    {
      grant.Revoke(now);
      await context.UserRoles.AddAsync(UserRole.Create(userId, grant.RoleId, currentUser.UserId, wanted[grant.RoleId], now), cancellationToken);
    }

    // the roles the person will hold together: a new pair of duties that should be kept apart needs a reason
    var keptIds = live.Where(g => wanted.ContainsKey(g.RoleId)).Select(g => g.RoleId).ToList();
    var beforeIds = live.Select(g => g.RoleId).Distinct().ToList();
    var afterIds = keptIds.Concat(addIds).Distinct().ToList();
    async Task<List<string>> CodesOf(List<RoleId> ids) => await context.RolePermissions.AsNoTracking()
      .Where(rp => ids.Contains(rp.RoleId))
      .Join(context.Permissions.AsNoTracking().Where(p => p.IsActive), rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code.Value)
      .Distinct().ToListAsync(cancellationToken);
    if (!roles.Values.Any(r => r.IsSuperAdmin && addIds.Contains(r.Id)))
    {
      var known = AccessMap.ConflictsIn(await CodesOf(beforeIds));
      var after = await CodesOf(afterIds);
      if (AccessMap.ConflictsIn(after).Any(c => !known.Contains(c)))
        await ConflictCheck.EnsureAcceptedAsync(activity, after, command.ConflictReason, ActivityTargetType.User, userId.Value, user.DisplayName.Value, cancellationToken);
    }

    var given = addIds.Select(id => roles[id].RoleName.Value).OrderBy(n => n).ToList();
    var taken = removeGrants.Select(g => roles.TryGetValue(g.RoleId, out var r) ? r.RoleName.Value : "a deleted role").OrderBy(n => n).ToList();
    var detail = string.Join("; ", new[]
    {
      given.Count > 0 ? $"Given: {string.Join(", ", given)}" : null,
      taken.Count > 0 ? $"Taken away: {string.Join(", ", taken)}" : null,
      redated.Count > 0 ? $"New end date: {string.Join(", ", redated.Select(g => $"{roles[g.RoleId].RoleName.Value} {(wanted[g.RoleId] is { } d ? d.ToString("dd'/'MM'/'yyyy") : "none")}"))}" : null,
    }.Where(p => p is not null));
    await activity.RecordAsync(ActivityAction.RolesChanged, ActivityTargetType.User, userId.Value, user.DisplayName.Value, detail, cancellationToken);

    // tokens issued before this change stop working at once
    await context.AccessChangedAsync(userId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<SetUserRolesCommandResult>.Success(new SetUserRolesCommandResult(addIds.Count + redated.Count, removeGrants.Count));
  }
}
