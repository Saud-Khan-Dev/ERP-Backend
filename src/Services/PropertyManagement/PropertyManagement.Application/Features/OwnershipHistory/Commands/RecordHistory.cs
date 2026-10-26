using FluentValidation;

public sealed record HistoryEntryResult(Guid Id, string EntryNo, HistoryEntryStatus Status, IReadOnlyList<string> TransferNos);

/// A clerk enters a property's ownership from the old records in one go. It is played forward now through the
/// transfer rules (nothing is written to the register) and waits for a second officer - or, while the PROPERTY_TRANSFER
/// rule is off and the clerk may approve, it is registered at once.
public sealed record RecordHistoryCommand(Guid PropertyId, IReadOnlyList<HistoryStepInput> Steps, string? Remarks)
  : ICommand<Result<HistoryEntryResult>>;

public class RecordHistoryCommandValidator : AbstractValidator<RecordHistoryCommand>
{
  public RecordHistoryCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Steps).NotEmpty().WithMessage("Enter at least the first owner(s) on record.");
    RuleForEach(x => x.Steps).NotNull().SetValidator(new HistoryStepInputValidator());
    RuleForEach(x => x.Steps).Must(s => s.DocumentIds is null || s.DocumentIds.Count == 0)
      .WithMessage("Papers are filed on a step once the history is saved (POST /ownership-history/{id}/steps/{step}/documents).");
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class RecordHistoryHandler(
    IApplicationDbContext context, OwnershipHistoryService history, CodeGenerator codes, ICurrentUser currentUser,
    IDutyRules dutyRules, IAuditTrail audit)
  : ICommandHandler<RecordHistoryCommand, Result<HistoryEntryResult>>
{
  public async Task<Result<HistoryEntryResult>> Handle(RecordHistoryCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var steps = command.Steps.Select(s => s.ToStep()).ToList();
    var historyContext = await history.ContextAsync(property, steps, cancellationToken);
    var approver = TransferRegistration.Approver(currentUser);

    var entry = OwnershipHistoryEntry.Record(
      HistoryEntryId.New(), await codes.NextAsync(CodeSequenceKeys.History, cancellationToken), steps, command.Remarks,
      TransferRegistration.ActorName(currentUser), historyContext, approver);
    await context.HistoryEntries.AddAsync(entry, cancellationToken);

    // two people pass every change of ownership while the PROPERTY_TRANSFER rule is on; with it off, an officer who may
    // approve registers what they entered in the same step
    var registerNow = !await dutyRules.IsOnAsync(DutyRules.PropertyTransfer, cancellationToken)
      && currentUser.HasPermission(PermissionCatalog.Property.Approve);

    if (!registerNow)
    {
      audit.Record(PropertyAuditActions.RecordedForApproval, entry,
        $"{entry.Steps.Count} step(s) from the old records; waits for another officer's approval");
      await context.SaveChangesAsync(cancellationToken);
      return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, []));
    }

    var transferNos = await HistoryRegistrar.RegisterAsync(entry, property, historyContext, context, codes, currentUser, audit,
      "Recorded and registered in one step (the two-person rule is off)", cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, transferNos));
  }
}
