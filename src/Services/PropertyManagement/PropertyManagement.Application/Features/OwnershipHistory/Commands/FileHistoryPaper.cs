using FluentValidation;

/// A paper for one step of a waiting ownership history (1 = the first owners): the deed, mutation, court order ...
/// It is filed with the entry and moves to the transfer (or first holding) the step becomes when it is registered.
public sealed record FileHistoryPaperCommand(Guid Id, int StepNo, Guid DocumentTypeId, DocumentDetailsInput Details, DocumentService.Upload File)
  : ICommand<Result<UploadDocumentCommandResult>>;

public class FileHistoryPaperCommandValidator : AbstractValidator<FileHistoryPaperCommand>
{
  public FileHistoryPaperCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.StepNo).GreaterThan(0);
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.File).NotNull();
    RuleFor(x => x.Details.Title).MaximumLength(200);
    RuleFor(x => x.Details.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Details.Description).MaximumLength(4000);
  }
}

public class FileHistoryPaperHandler(
    IApplicationDbContext context, OwnershipHistoryService history, MasterLookup masters, DocumentService documents, IAuditTrail audit)
  : ICommandHandler<FileHistoryPaperCommand, Result<UploadDocumentCommandResult>>
{
  public async Task<Result<UploadDocumentCommandResult>> Handle(FileHistoryPaperCommand command, CancellationToken cancellationToken)
  {
    var entry = await history.LoadAsync(command.Id, cancellationToken);
    if (!entry.IsWaiting)
      throw new DomainException($"{entry.EntryNo.Value} is {entry.Status.ToString().ToLowerInvariant()}; papers are added to a registered history's transfers.");
    if (command.StepNo > entry.Steps.Count)
      throw new DomainException($"{entry.EntryNo.Value} has steps 1 to {entry.Steps.Count}; there is no step {command.StepNo}.");

    var documentType = await masters.GetAsync<DocumentType>(command.DocumentTypeId, cancellationToken);

    // every check (entry waiting on this property, document type scope) runs before the file is stored
    var target = await documents.PropertyTargetAsync(entry.PropertyId, DocumentEntityType.OwnershipHistory, entry.Id.Value, cancellationToken);
    var document = await documents.CreateAsync(target, documentType, command.File, command.Details.ToDetails(), cancellationToken);
    entry.AttachPaper(command.StepNo, document.Id.Value);

    audit.Record(PropertyAuditActions.RecordedForApproval, entry, $"Paper for step {command.StepNo}: {document.OriginalFileName}");
    await context.SaveChangesAsync(cancellationToken);
    return Result<UploadDocumentCommandResult>.Success(new UploadDocumentCommandResult(document.Id.Value, document.VersionNo));
  }
}
