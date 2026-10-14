using FluentValidation;

public sealed record UpdateDocumentDetailsCommandResult(bool IsSuccess);

/// Metadata only — a different file is a new version. The document type may change only to another of the same
/// scope (PROPERTY or OWNER).
public sealed record UpdateDocumentDetailsCommand(Guid Id, Guid DocumentTypeId, DocumentDetailsInput Details) : ICommand<Result<UpdateDocumentDetailsCommandResult>>;

public class UpdateDocumentDetailsCommandValidator : AbstractValidator<UpdateDocumentDetailsCommand>
{
  public UpdateDocumentDetailsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.Details).NotNull();
    RuleFor(x => x.Details.Title).MaximumLength(200);
    RuleFor(x => x.Details.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Details.Description).MaximumLength(4000);
  }
}
