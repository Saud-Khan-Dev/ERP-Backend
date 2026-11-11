using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Makes an account a taxpayer's by linking it to the person's owner record in Property - or unlinks it (null). The
/// person then sees their own properties and taxes (Taxation /my) and needs no roles. The `own` claim follows at once:
/// the account's earlier tokens stop working.
public sealed record LinkOwnerCommand(Guid UserId, Guid? OwnerId) : ICommand<Result<LinkOwnerCommandResult>>;

public sealed record LinkOwnerCommandResult(Guid? OwnerId);

public class LinkOwnerCommandValidator : AbstractValidator<LinkOwnerCommand>
{
  public LinkOwnerCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.OwnerId).Must(id => id != Guid.Empty).WithMessage("Choose the person's owner record.");
  }
}

public class LinkOwnerHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity)
  : ICommandHandler<LinkOwnerCommand, Result<LinkOwnerCommandResult>>
{
  public async Task<Result<LinkOwnerCommandResult>> Handle(LinkOwnerCommand command, CancellationToken cancellationToken)
  {
    var userId = UserId.Of(command.UserId);
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");
    // linking your own account to an owner would show you that person's taxes
    guard.EnsureNotSelf(userId, "link an owner record to");
    await guard.EnsureCanActOnAsync(userId, "link", cancellationToken);

    if (user.OwnerId == command.OwnerId)
      return Result<LinkOwnerCommandResult>.Success(new LinkOwnerCommandResult(user.OwnerId));

    if (command.OwnerId is { } ownerId)
    {
      var other = await context.Users.IgnoreQueryFilters().AsNoTracking()
        .Where(u => u.Id != userId && u.OwnerId == ownerId)
        .Select(u => new { Name = u.DisplayName.Value, Username = u.Username.Value, u.DeletedAt })
        .FirstOrDefaultAsync(cancellationToken);
      if (other is not null)
        return Result<LinkOwnerCommandResult>.Failure(other.DeletedAt is null
          ? $"This owner is already linked to the account of {other.Name} ({other.Username}). Unlink it there first."
          : $"This owner is linked to the deleted account {other.Username}. Restore that account and unlink it first.");
    }

    var before = user.OwnerId;
    user.LinkOwner(command.OwnerId);

    await activity.RecordAsync(
      command.OwnerId is null ? ActivityAction.OwnerUnlinked : ActivityAction.OwnerLinked,
      ActivityTargetType.User, user.Id.Value, user.DisplayName.Value,
      command.OwnerId is null ? $"Was Property owner {before}" : $"Property owner {command.OwnerId}",
      cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<LinkOwnerCommandResult>.Success(new LinkOwnerCommandResult(user.OwnerId));
  }
}
