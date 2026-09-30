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

/// Files a scanned document. Either PropertyId (optionally naming a sub-record through EntityType +
/// EntityId) or OwnerId (an owner's own papers) is set.
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
    RuleFor(x => x).Must(x => x.PropertyId.HasValue || x.OwnerId.HasValue)
      .WithMessage("A document is filed under a property or an owner.");
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.EntityType).IsInEnum();
    RuleFor(x => x.File).NotNull();
    RuleFor(x => x.Details.Title).MaximumLength(200);
    RuleFor(x => x.Details.ReferenceNo).MaximumLength(100);
  }
}
