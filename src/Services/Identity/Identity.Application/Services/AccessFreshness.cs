using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

/// Raising access stamps: whatever changes what an account may do calls one of these in the same unit of work, and
/// every token issued before stops working (the services ask DatabaseTokenStateChecker through /auth/token-state).
public static class AccessStamps
{
  public static async Task AccessChangedAsync(this IApplicationDbContext context, UserId userId, CancellationToken cancellationToken)
  {
    var user = context.Users.Local.FirstOrDefault(u => u.Id == userId)
      ?? await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    user?.AccessChanged();
  }

  /// Every account holding the role now (a change to the role's access, switching it off, renaming nothing).
  public static async Task HoldersAccessChangedAsync(this IApplicationDbContext context, RoleId roleId, CancellationToken cancellationToken)
  {
    var holderIds = await context.UserRoles.AsNoTracking()
      .Where(ur => ur.RoleId == roleId && ur.RevokedAt == null)
      .Select(ur => ur.UserId)
      .Distinct()
      .ToListAsync(cancellationToken);
    if (holderIds.Count == 0)
      return;
    var holders = await context.Users.Where(u => holderIds.Contains(u.Id)).ToListAsync(cancellationToken);
    foreach (var holder in holders)
      holder.AccessChanged();
  }
}

/// Identity's own check of a token (its services ask through `GET /auth/token-state`): the sign-in session is still
/// open - a session renewed by refresh lives on in its successor - the account is on, and the token's access stamp is
/// the account's current one.
public sealed class DatabaseTokenStateChecker(IApplicationDbContext context) : ITokenStateChecker
{
  public async Task<TokenVerdict> CheckAsync(ClaimsPrincipal user, string rawToken, CancellationToken cancellationToken)
  {
    if (!Guid.TryParse(user.FindFirst(ErpClaimTypes.SessionId)?.Value, out var sid)
        || !Guid.TryParse(user.FindFirst(ErpClaimTypes.UserId)?.Value, out var uid)
        || !long.TryParse(user.FindFirst(ErpClaimTypes.AccessStamp)?.Value, out var stamp))
      return TokenVerdict.Stale("Signed in before this version - sign in again");

    var now = DateTime.UtcNow;
    var session = await context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SessionId.Of(sid), cancellationToken);
    // a refresh rotates the session: the token stays good while the sign-in it belongs to goes on
    for (var hop = 0; session is { ReplacedBySessionId: { } next, RevokedReason: "Rotated" } && hop < 50; hop++)
      session = await context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == next, cancellationToken);
    if (session is null || !session.IsActive(now))
      return TokenVerdict.Stale(session?.RevokedReason is { } reason && reason != "Rotated" ? $"Signed out: {reason}" : "Signed out");

    var account = await context.Users.IgnoreQueryFilters().AsNoTracking()
      .Where(u => u.Id == UserId.Of(uid))
      .Select(u => new { u.IsActive, u.DeletedAt, u.AccessStamp })
      .FirstOrDefaultAsync(cancellationToken);
    if (account is null || account.DeletedAt is not null || !account.IsActive)
      return TokenVerdict.Stale("The account was switched off");
    if (account.AccessStamp != stamp)
      return TokenVerdict.Stale("Your access has changed");
    return TokenVerdict.Ok;
  }
}

/// Identity's own reading of the duty-rule switches (the other services ask `GET /duty-rules/state`).
public sealed class DatabaseDutyRules(IApplicationDbContext context) : IDutyRules
{
  public async Task<bool> IsOnAsync(string code, CancellationToken cancellationToken = default)
  {
    var row = await context.DutyRuleSwitches.AsNoTracking().FirstOrDefaultAsync(r => r.Id == code, cancellationToken);
    return row?.IsOn ?? true;
  }
}
