using FluentValidation;

/// ReferenceNo: the number printed on the paper - notesheet or letter no. on a property paper; the CNIC, passport,
/// NTN or certificate no. on an owner's paper.
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

/// Two routes, one pipeline:
/// - a property's paper: PropertyId set, OwnerId null; EntityType + EntityId name the record on the property it
///   belongs to (PROPERTY by default). The document type must apply to PROPERTY.
/// - an owner's own paper: OwnerId set and PropertyId null (an owner's paper belongs to no property). The document
///   type must apply to OWNER and the owner must be active.
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
    RuleFor(x => x.PropertyId).NotEmpty().When(x => x.OwnerId is null)
      .WithMessage("A property document needs the property it belongs to.");
    RuleFor(x => x.PropertyId).Null().When(x => x.OwnerId is not null)
      .WithMessage("An owner's document is not filed under a property - leave propertyId out.");
    RuleFor(x => x.EntityType).NotEqual(DocumentEntityType.Owner).When(x => x.OwnerId is null)
      .WithMessage("An owner's paper is filed on the owner (POST /owners/{id}/documents), not on a property.");
    RuleFor(x => x.DocumentTypeId).NotEmpty();
    RuleFor(x => x.EntityType).IsInEnum();
    RuleFor(x => x.File).NotNull();
    RuleFor(x => x.Details.Title).MaximumLength(200);
    RuleFor(x => x.Details.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Details.Description).MaximumLength(4000);
  }
}
