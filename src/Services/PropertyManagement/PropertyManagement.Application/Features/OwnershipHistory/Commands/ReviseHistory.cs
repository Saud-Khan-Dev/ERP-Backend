using FluentValidation;

/// The clerk corrects a waiting ownership history (steps, shares, dates, papers per step); it is played forward again.
public sealed record ReviseHistoryCommand(Guid Id, IReadOnlyList<HistoryStepInput> Steps, string? Remarks) : ICommand<Result<HistoryEntryResult>>;

public class ReviseHistoryCommandValidator : AbstractValidator<ReviseHistoryCommand>
{
  public ReviseHistoryCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Steps).NotEmpty().WithMessage("Enter at least the first owner(s) on record.");
    RuleForEach(x => x.Steps).NotNull().SetValidator(new HistoryStepInputValidator());
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class ReviseHistoryHandler(IApplicationDbContext context, OwnershipHistoryService history, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<ReviseHistoryCommand, Result<HistoryEntryResult>>
{
  public async Task<Result<HistoryEntryResult>> Handle(ReviseHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(entry.PropertyId.Value, cancellationToken);
    var steps = command.Steps.Select(s => s.ToStep()).ToList();

    await history.EnsureOwnPapersAsync(entry, steps, cancellationToken);
    entry.Revise(steps, command.Remarks, await history.ContextAsync(property, steps, cancellationToken), TransferRegistration.Approver(currentUser));

    audit.Record(PropertyAuditActions.RecordedForApproval, entry, $"Changed while waiting · {steps.Count} step(s)");
    await context.SaveChangesAsync(cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
  }
}
