using FluentValidation;

/// A waiting ownership history is closed without changing anything: an approving officer rejects it (Reject), or the
/// clerk takes it back (Withdraw). A reason is required either way.
public sealed record CloseHistoryCommand(Guid Id, bool Reject, string Reason) : ICommand<Result<HistoryEntryResult>>;

public class CloseHistoryCommandValidator : AbstractValidator<CloseHistoryCommand>
{
  public CloseHistoryCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason, so the clerk knows what to put right.").MaximumLength(4000);
  }
}

public class CloseHistoryHandler(IApplicationDbContext context, OwnershipHistoryService history, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<CloseHistoryCommand, Result<HistoryEntryResult>>
{
  public async Task<Result<HistoryEntryResult>> Handle(CloseHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    await context.LoadPropertyAsync(entry.PropertyId.Value, cancellationToken);
    var by = TransferRegistration.ActorName(currentUser);

    if (command.Reject)
    {
      entry.Reject(command.Reason, by, currentUser.UserId, PropertyWriteGuard.Today());
      audit.Record(PropertyAuditActions.Rejected, entry, command.Reason);
    }
    else
    {
      entry.Withdraw(command.Reason, by, currentUser.UserId, PropertyWriteGuard.Today());
      audit.Record(PropertyAuditActions.Withdrawn, entry, command.Reason);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }
}
