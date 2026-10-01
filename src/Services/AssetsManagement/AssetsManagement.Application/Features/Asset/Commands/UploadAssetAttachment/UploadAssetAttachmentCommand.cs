using FluentValidation;

public sealed record UploadAssetAttachmentCommandResult(Guid Id, string OriginalFileName, long FileSize);

/// A real file upload: the bytes go to the file store under /asset-files/{asset code}/, the database keeps the
/// metadata (name, type, size, SHA-256). AttachmentType defaults to Image for image/* files, Document otherwise.
public sealed record UploadAssetAttachmentCommand(
  Guid AssetId,
  Stream Content,
  string FileName,
  string? ContentType,
  long Length,
  AttachmentType? AttachmentType = null,
  string? Title = null,
  bool IsPrimaryImage = false) : ICommand<Result<UploadAssetAttachmentCommandResult>>;

public class UploadAssetAttachmentCommandValidator : AbstractValidator<UploadAssetAttachmentCommand>
{
  public UploadAssetAttachmentCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
    RuleFor(x => x.Title).MaximumLength(200);
    RuleFor(x => x.AttachmentType).IsInEnum().When(x => x.AttachmentType.HasValue);
  }
}
