using Microsoft.EntityFrameworkCore;

public sealed record AccessReviewDecisionDto(Guid UserId, string UserName, string Outcome, string AccessText, DateTime? LastSignInAt, string? Note,
  DateTime DecidedAt, string DecidedByName);

public sealed record AccessReviewDto(Guid Id, DateTime StartedAt, string StartedByName, int AccountCount, DateTime? ClosedAt, string? ClosedByName,
  string? Note, IReadOnlyList<AccessReviewDecisionDto> Decisions);

/// The review in progress (if any), the ones signed off before, and when the next is due.
public sealed record AccessReviewsDto(AccessReviewDto? Open, IReadOnlyList<AccessReviewDto> Closed, DateOnly DueOn, bool Overdue);

public static class AccessReviewState
{
  /// When the next review is due: the last sign-off plus the review interval; never reviewed → due now.
  public static async Task<OversightReviewDto> OfAsync(IApplicationDbContext context, SecuritySettings settings, DateTime now, CancellationToken cancellationToken)
  {
    var open = await context.AccessReviews.AsNoTracking().Where(r => r.ClosedAt == null).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
    var lastClosed = await context.AccessReviews.AsNoTracking().Where(r => r.ClosedAt != null).MaxAsync(r => r.ClosedAt, cancellationToken);
    var due = DueOn(lastClosed, settings, now);
    var decided = open is null ? 0 : await context.AccessReviewDecisions.AsNoTracking().CountAsync(d => d.ReviewId == open.Id, cancellationToken);
    var toDecide = open is null ? 0 : await context.Users.AsNoTracking().CountAsync(u => u.IsActive, cancellationToken);
    return new OversightReviewDto(open?.Id, open?.StartedAt, decided, toDecide, lastClosed, due, open is null && due <= DateOnly.FromDateTime(now));
  }

  public static DateOnly DueOn(DateTime? lastClosed, SecuritySettings settings, DateTime now) =>
    lastClosed is { } last ? DateOnly.FromDateTime(last.AddMonths(settings.AccessReviewMonths)) : DateOnly.FromDateTime(now);

  public static AccessReviewDto ToDto(AccessReview r, IEnumerable<AccessReviewDecision> decisions) => new(r.Id, r.StartedAt, r.StartedByName,
    r.AccountCount, r.ClosedAt, r.ClosedByName, r.Note,
    decisions.OrderBy(d => d.UserName).Select(d => new AccessReviewDecisionDto(d.UserId.Value, d.UserName, d.Outcome.ToString(), d.AccessText,
      d.LastSignInAt, d.Note, d.DecidedAt, d.DecidedByName)).ToList());
}

public sealed record GetAccessReviewsQuery : IQuery<Result<AccessReviewsDto>>;

public class GetAccessReviewsHandler(IApplicationDbContext context, ISecuritySettingsProvider settingsProvider) : IQueryHandler<GetAccessReviewsQuery, Result<AccessReviewsDto>>
{
  public async Task<Result<AccessReviewsDto>> Handle(GetAccessReviewsQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var settings = await settingsProvider.GetAsync(cancellationToken);
    var reviews = await context.AccessReviews.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(12).ToListAsync(cancellationToken);
    var ids = reviews.Select(r => r.Id).ToList();
    var decisions = (await context.AccessReviewDecisions.AsNoTracking().Where(d => ids.Contains(d.ReviewId)).ToListAsync(cancellationToken)).ToLookup(d => d.ReviewId);
    var open = reviews.FirstOrDefault(r => r.ClosedAt is null);
    var lastClosed = reviews.Where(r => r.ClosedAt is not null).Max(r => r.ClosedAt);
    var due = AccessReviewState.DueOn(lastClosed, settings, now);
    return Result<AccessReviewsDto>.Success(new AccessReviewsDto(
      open is null ? null : AccessReviewState.ToDto(open, decisions[open.Id]),
      reviews.Where(r => r.ClosedAt is not null).Select(r => AccessReviewState.ToDto(r, decisions[r.Id])).ToList(),
      due,
      open is null && due <= DateOnly.FromDateTime(now)));
  }
}

/// Starts a review of every active account. One at a time.
public sealed record StartAccessReviewCommand : ICommand<Result<Guid>>;

public class StartAccessReviewHandler(IApplicationDbContext context, ICurrentUser currentUser, IActivityRecorder activity) : ICommandHandler<StartAccessReviewCommand, Result<Guid>>
{
  public async Task<Result<Guid>> Handle(StartAccessReviewCommand command, CancellationToken cancellationToken)
  {
    if (await context.AccessReviews.AnyAsync(r => r.ClosedAt == null, cancellationToken))
      return Result<Guid>.Failure("A review is already in progress - finish it first.");
    var count = await context.Users.CountAsync(u => u.IsActive, cancellationToken);
    var review = AccessReview.Start(currentUser.UserId, currentUser.AuditName, count, DateTime.UtcNow);
    await context.AccessReviews.AddAsync(review, cancellationToken);
    await activity.RecordAsync(ActivityAction.AccessReviewStarted, ActivityTargetType.System, review.Id, "Access review", $"{count} active accounts to review", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<Guid>.Success(review.Id);
  }
}

/// One account decided: kept as it is, changed (the change itself is made with the usual acts first), or switched off
/// here and now. Your own account is decided by another administrator.
public sealed record DecideAccessReviewCommand(Guid ReviewId, Guid UserId, ReviewOutcome Outcome, string? Note) : ICommand<Result<AccessReviewDecisionDto>>;

public class DecideAccessReviewHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser, IActivityRecorder activity)
  : ICommandHandler<DecideAccessReviewCommand, Result<AccessReviewDecisionDto>>
{
  public async Task<Result<AccessReviewDecisionDto>> Handle(DecideAccessReviewCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var review = await context.AccessReviews.FirstOrDefaultAsync(r => r.Id == command.ReviewId, cancellationToken)
      ?? throw new DomainException("This review was not found.");
    if (!review.IsOpen)
      return Result<AccessReviewDecisionDto>.Failure("This review is already signed off.");
    var userId = UserId.Of(command.UserId);
    guard.EnsureNotSelf(userId, "review the access of");
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found - it may have been deleted.");

    var map = await AccessMap.BuildAsync(context, cancellationToken);
    var account = map.Accounts[userId];
    var accessText = string.Join(" · ", new[]
    {
      account.Roles.Count == 0 ? "No roles" : string.Join(", ", account.Roles.Where(r => r.Assignment.IsLive(now)).Select(r => r.Role.RoleName.Value)),
      account.Denied.Count + account.Permissions.Values.Count(g => g.Sources.Any(s => s.StartsWith("Exception"))) is var n && n > 0 ? $"{n} exception{(n == 1 ? "" : "s")}" : null,
      account.Scopes.Count == 0 ? null : string.Join(", ", account.Scopes.Select(s => s.Label))
    }.Where(x => x is not null));

    if (command.Outcome == ReviewOutcome.SwitchedOff && user.IsActive)
    {
      await guard.EnsureCanActOnAsync(userId, "switch off", cancellationToken);
      await guard.EnsureNotLastSuperAdminAsync(userId, "switching it off", cancellationToken);
      user.Deactivate();
      foreach (var session in await context.Sessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync(cancellationToken))
        session.Revoke(now, "Switched off in an access review");
      await activity.RecordAsync(ActivityAction.UserDeactivated, ActivityTargetType.User, userId.Value, user.Username.Value, "In the access review", cancellationToken);
    }

    var existing = await context.AccessReviewDecisions.FirstOrDefaultAsync(d => d.ReviewId == review.Id && d.UserId == userId, cancellationToken);
    if (existing is not null)
      context.AccessReviewDecisions.Remove(existing);
    var decision = AccessReviewDecision.Decide(review.Id, userId, user.DisplayName.Value, command.Outcome, accessText, user.LastLoginAt, command.Note,
      currentUser.UserId, currentUser.AuditName, now);
    await context.AccessReviewDecisions.AddAsync(decision, cancellationToken);
    await activity.RecordAsync(ActivityAction.AccessReviewDecided, ActivityTargetType.User, userId.Value, user.Username.Value,
      $"{AuditProfile.Words(command.Outcome.ToString())}{(string.IsNullOrWhiteSpace(command.Note) ? "" : " · " + command.Note.Trim())}", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<AccessReviewDecisionDto>.Success(new AccessReviewDecisionDto(userId.Value, decision.UserName, decision.Outcome.ToString(), decision.AccessText,
      decision.LastSignInAt, decision.Note, decision.DecidedAt, decision.DecidedByName));
  }
}

/// Signs the review off once every active account is decided (the reviewer's own account excepted: another
/// administrator reviews it - it is named in the note when nobody did).
public sealed record CloseAccessReviewCommand(Guid ReviewId, string? Note) : ICommand<Result<bool>>;

public class CloseAccessReviewHandler(IApplicationDbContext context, ICurrentUser currentUser, IActivityRecorder activity) : ICommandHandler<CloseAccessReviewCommand, Result<bool>>
{
  public async Task<Result<bool>> Handle(CloseAccessReviewCommand command, CancellationToken cancellationToken)
  {
    var review = await context.AccessReviews.FirstOrDefaultAsync(r => r.Id == command.ReviewId, cancellationToken)
      ?? throw new DomainException("This review was not found.");
    var decided = await context.AccessReviewDecisions.AsNoTracking().Where(d => d.ReviewId == review.Id).Select(d => d.UserId).ToListAsync(cancellationToken);
    var me = currentUser.UserId is { } id ? UserId.Of(id) : null;
    var undecided = await context.Users.AsNoTracking()
      .Where(u => u.IsActive && !decided.Contains(u.Id) && (me == null || u.Id != me))
      .Select(u => u.DisplayName.Value)
      .ToListAsync(cancellationToken);
    if (undecided.Count > 0)
      return Result<bool>.Failure($"{undecided.Count} account{(undecided.Count == 1 ? " is" : "s are")} not decided yet: {string.Join(", ", undecided.Take(5))}{(undecided.Count > 5 ? " ..." : "")}.");
    var selfUndecided = me is not null && !decided.Contains(me);
    var note = string.Join(" · ", new[] { command.Note?.Trim(), selfUndecided ? $"{currentUser.AuditName}'s own account was not reviewed by another administrator" : null }
      .Where(x => !string.IsNullOrWhiteSpace(x)));
    review.Close(currentUser.AuditName, note, DateTime.UtcNow);
    await activity.RecordAsync(ActivityAction.AccessReviewClosed, ActivityTargetType.System, review.Id, "Access review",
      $"{decided.Count} accounts decided{(note.Length > 0 ? " · " + note : "")}", cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
