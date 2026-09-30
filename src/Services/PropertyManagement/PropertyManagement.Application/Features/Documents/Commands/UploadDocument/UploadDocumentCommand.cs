using FluentValidation;

public sealed record DocumentDetailsInput(
  string? Title = null,
  DateOnly? DocumentDate = null,
  string? ReferenceNo = null,
  string? Description = null,
  bool IsConfidential = false)
{
  public PropertyDocument.Details ToDetails() => new(Title, DocumentDate, ReferenceNo, Description, IsConfidential);
}

public sealed record UploadDocumentCommandResult(Guid Id, int VersionNo);

/// Files a scanned document in a property's file (property_id is always set). EntityType + EntityId name
/// the sub-record it belongs to; with OwnerId it is one of that owner's own papers (CNIC copy ...).
public sealed record UploadDocumentCommand(
  Guid? PropertyId,
  Guid? OwnerId,
  Guid DocumentTypeId,
  DocumentEntityType EntityType,
  Guid? EntityId,
  DocumentDetailsInput Details,
  DocumentService.Upload File) : ICommand<Result<UploadDocumentCommandResult>>;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
  public UploadDocumentCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty()
      .WithMessage("A document is always filed under a property (property_document.property_id).");
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.EntityType).IsInEnum();
    RuleFor(x => x.File).NotNull();
    RuleFor(x => x.Details.Title).MaximumLength(200);
    RuleFor(x => x.Details.ReferenceNo).MaximumLength(100);
  }
}
