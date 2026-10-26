using Microsoft.EntityFrameworkCore;

/// A second officer registers a waiting ownership history: the owners and transfers it describes enter the register.
public sealed record RegisterHistoryCommand(Guid Id) : ICommand<Result<HistoryEntryResult>>;

public class RegisterHistoryHandler(
    IApplicationDbContext context, OwnershipHistoryService history, CodeGenerator codes, ICurrentUser currentUser,
    IDutyRules dutyRules, IAuditTrail audit)
  : ICommandHandler<RegisterHistoryCommand, Result<HistoryEntryResult>>
{
  public async Task<Result<HistoryEntryResult>> Handle(RegisterHistoryCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);

    // the PROPERTY_TRANSFER rule: the clerk who entered the history is not the one who registers it
    var refusal = await dutyRules.SeparationRefusalAsync(DutyRules.PropertyTransfer, entry.CreatedBy, currentUser.UserId,
      $"register {entry.EntryNo.Value}, which you entered", cancellationToken);
    if (refusal is not null)
      throw new DomainException(refusal);

    var property = await context.LoadPropertyAsync(entry.PropertyId.Value, cancellationToken);
    var historyContext = await history.ContextAsync(property, entry.Steps, cancellationToken);

    var transferNos = await HistoryRegistrar.RegisterAsync(entry, property, historyContext, context, codes, currentUser, audit,
      $"Entered by {entry.RecordedBy} on {entry.RecordedOn:yyyy-MM-dd}", cancellationToken);
    return Result<HistoryEntryResult>.Success(new HistoryEntryResult(entry.Id.Value, entry.EntryNo.Value, entry.Status, transferNos));
  }
}

/// Writes a registered history: new TRF numbers, the transfers and holdings, the present holdings that took on their
/// history, and every step's papers moved to the record the step became.
public static class HistoryRegistrar
{
  public static async Task<IReadOnlyList<string>> RegisterAsync(
      OwnershipHistoryEntry entry,
      Property property,
      HistoryContext historyContext,
      IApplicationDbContext context,
      CodeGenerator codes,
      ICurrentUser currentUser,
      IAuditTrail audit,
      string detail,
      CancellationToken cancellationToken)
  {
    var numbers = new List<BusinessCode>();
    for (var i = 0; i < entry.TransferCount; i++)
      numbers.Add(await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken));

    var registration = entry.Register(historyContext, numbers, TransferRegistration.Approver(currentUser));

    await context.Transfers.AddRangeAsync(registration.Transfers, cancellationToken);
    await context.Ownerships.AddRangeAsync(registration.NewRows, cancellationToken);

    var paperIds = registration.Papers.Select(p => DocumentId.Of(p.DocumentId)).ToList();
    var papers = await context.Documents.Where(d => paperIds.Contains(d.Id) && d.IsActive).ToListAsync(cancellationToken);
    foreach (var (documentId, entityType, entityId) in registration.Papers)
      papers.FirstOrDefault(d => d.Id.Value == documentId)?.MoveToRegisteredRecord(entityType, entityId);

    // other changes of ownership on the property made meanwhile fail the save (row version)
    context.Touch(property);

    var transferNos = registration.Transfers.Select(t => t.TransferNo.Value).ToList();
    audit.Record(PropertyAuditActions.Registered, entry,
      $"{detail} · {entry.Steps.Count} step(s){(transferNos.Count > 0 ? " · " + string.Join(", ", transferNos) : "")}");
    foreach (var transfer in registration.Transfers)
      audit.Record(PropertyAuditActions.Approved, transfer, $"From the old records with {entry.EntryNo.Value}");

    await context.SaveChangesAsync(cancellationToken);
    return transferNos;
  }
}
