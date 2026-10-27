using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Deleting one's own account: nobody deletes their own account. The person asks (with the reason); a senior who may
// delete accounts (IAM_USERS.DELETE, never the person themself) approves - the account is deleted through the same
// command as Administration › Users › Delete (soft: restorable within the recovery period) - or turns it down with why.

public sealed record DeletionRequestDto(
  Guid Id,
  Guid UserId,
  /// The person's name when they asked.
  string DisplayName,
  string? Username,
  string Reason,
  DateTime RequestedAt,
  DeletionRequestStatus Status,
  string? DecidedByName,
  DateTime? DecidedAt,
  string? Remarks);

public sealed record AskAccountDeletionCommand(string? Reason) : ICommand<Result<DeletionRequestDto>>;
public sealed record WithdrawAccountDeletionCommand : ICommand<Result<bool>>;
/// The signed-in person's latest request (null when they never asked).
public sealed record GetMyDeletionRequestQuery : IQuery<Result<DeletionRequestDto?>>;
/// Administration: requests, waiting ones first (oldest first), then the decided ones newest first.
public sealed record GetDeletionRequestsQuery(DeletionRequestStatus? Status) : IQuery<Result<IReadOnlyList<DeletionRequestDto>>>;
public sealed record DecideDeletionRequestCommand(Guid Id, bool Approve, string? Remarks) : ICommand<Result<bool>>;

public class AskAccountDeletionCommandValidator : AbstractValidator<AskAccountDeletionCommand>
{
  public AskAccountDeletionCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why the account should be deleted.").MaximumLength(1000);
}

public class DecideDeletionRequestCommandValidator : AbstractValidator<DecideDeletionRequestCommand>
{
  public DecideDeletionRequestCommandValidator()
  {
    RuleFor(x => x.Remarks).MaximumLength(1000);
    RuleFor(x => x.Remarks).NotEmpty().When(x => !x.Approve).WithMessage("Say why the account is not deleted, so the person knows.");
  }
}

public class DeletionRequestHandlers(IApplicationDbContext context, ICurrentUser currentUser, IActivityRecorder activity, ISender sender) :
  ICommandHandler<AskAccountDeletionCommand, Result<DeletionRequestDto>>,
  ICommandHandler<WithdrawAccountDeletionCommand, Result<bool>>,
  IQueryHandler<GetMyDeletionRequestQuery, Result<DeletionRequestDto?>>,
  IQueryHandler<GetDeletionRequestsQuery, Result<IReadOnlyList<DeletionRequestDto>>>,
  ICommandHandler<DecideDeletionRequestCommand, Result<bool>>
{
  public async Task<Result<DeletionRequestDto>> Handle(AskAccountDeletionCommand command, CancellationToken cancellationToken)
  {
    var me = Me();
    if (await context.AccountDeletionRequests.AnyAsync(r => r.UserId == me && r.Status == DeletionRequestStatus.Pending, cancellationToken))
      return Result<DeletionRequestDto>.Failure("Your request is already waiting for an administrator. Withdraw it first to ask again.");
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == me, cancellationToken) ?? throw new UserNotFoundException("Your account was not found.");
    var request = AccountDeletionRequest.Ask(user, command.Reason, DateTime.UtcNow);
    context.AccountDeletionRequests.Add(request);
    await activity.RecordAsync(ActivityAction.DeletionRequested, ActivityTargetType.User, user.Id.Value, user.Username.Value, request.Reason, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<DeletionRequestDto>.Success(await DtoAsync(request, cancellationToken));
  }

  public async Task<Result<bool>> Handle(WithdrawAccountDeletionCommand command, CancellationToken cancellationToken)
  {
    var me = Me();
    var request = await context.AccountDeletionRequests.FirstOrDefaultAsync(r => r.UserId == me && r.Status == DeletionRequestStatus.Pending, cancellationToken)
      ?? throw new DomainException("You have no request waiting.");
    request.Withdraw(DateTime.UtcNow);
    await activity.RecordAsync(ActivityAction.DeletionRequestWithdrawn, ActivityTargetType.User, me.Value, currentUser.Username, null, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }

  public async Task<Result<DeletionRequestDto?>> Handle(GetMyDeletionRequestQuery query, CancellationToken cancellationToken)
  {
    var me = Me();
    var latest = await context.AccountDeletionRequests.AsNoTracking().Where(r => r.UserId == me)
      .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync(cancellationToken);
    return Result<DeletionRequestDto?>.Success(latest is null ? null : await DtoAsync(latest, cancellationToken));
  }

  public async Task<Result<IReadOnlyList<DeletionRequestDto>>> Handle(GetDeletionRequestsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.AccountDeletionRequests.AsNoTracking();
    if (query.Status is { } status)
      rows = rows.Where(r => r.Status == status);
    var list = await rows.OrderByDescending(r => r.Status == DeletionRequestStatus.Pending).ThenByDescending(r => r.RequestedAt).Take(500).ToListAsync(cancellationToken);
    var pending = list.Where(r => r.IsPending).OrderBy(r => r.RequestedAt).ToList();
    var ordered = pending.Concat(list.Where(r => !r.IsPending)).ToList();
    var usernames = await UsernamesAsync(ordered.Select(r => r.UserId), cancellationToken);
    return Result<IReadOnlyList<DeletionRequestDto>>.Success(ordered.Select(r => Dto(r, usernames.GetValueOrDefault(r.UserId.Value))).ToList());
  }

  public async Task<Result<bool>> Handle(DecideDeletionRequestCommand command, CancellationToken cancellationToken)
  {
    var request = await context.AccountDeletionRequests.FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken)
      ?? throw new UserNotFoundException("This request was not found.");
    var now = DateTime.UtcNow;
    if (!command.Approve)
    {
      request.Reject(currentUser.UserId, currentUser.AuditName, command.Remarks, now);
      await activity.RecordAsync(ActivityAction.DeletionRequestRejected, ActivityTargetType.User, request.UserId.Value, request.UserName, command.Remarks, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      return Result<bool>.Success(true);
    }

    request.Approve(currentUser.UserId, currentUser.AuditName, command.Remarks, now);
    // the same delete as Administration › Users (not one's own, not the last Super Administrator, sessions ended); it saves
    // the approval with it
    await sender.Send(new DeleteUserCommand(request.UserId.Value), cancellationToken);
    return Result<bool>.Success(true);
  }

  private UserId Me() => currentUser.UserId is { } id ? UserId.Of(id) : throw new UnauthorizedAccessException("Sign in first.");

  private async Task<DeletionRequestDto> DtoAsync(AccountDeletionRequest request, CancellationToken cancellationToken) =>
      Dto(request, (await UsernamesAsync([request.UserId], cancellationToken)).GetValueOrDefault(request.UserId.Value));

  private async Task<Dictionary<Guid, string>> UsernamesAsync(IEnumerable<UserId> ids, CancellationToken cancellationToken)
  {
    var list = ids.Distinct().ToList();
    return await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => list.Contains(u.Id))
      .ToDictionaryAsync(u => u.Id.Value, u => u.Username.Value, cancellationToken);
  }

  private static DeletionRequestDto Dto(AccountDeletionRequest r, string? username) =>
      new(r.Id, r.UserId.Value, r.UserName, username, r.Reason, r.RequestedAt, r.Status, r.DecidedByName, r.DecidedAt, r.Remarks);
}
