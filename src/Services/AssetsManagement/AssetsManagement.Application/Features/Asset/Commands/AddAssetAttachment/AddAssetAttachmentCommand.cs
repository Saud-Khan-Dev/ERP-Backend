using FluentValidation;

/// Metadata of a file already placed in object storage (the API does not stream bytes through the database).
public sealed record AssetAttachmentInput(
  AttachmentType AttachmentType,
  string OriginalFileName,
  string StoredFileName,
  string MimeType,
  long FileSize,
  string StoragePath,
  string? ChecksumSha256 = null,
  bool IsPrimaryImage = false);

public sealed record AddAssetAttachmentCommandResult(Guid Id);

public sealed record AddAssetAttachmentCommand(Guid AssetId, AssetAttachmentInput Attachment) : ICommand<Result<AddAssetAttachmentCommandResult>>;

public class AssetAttachmentInputValidator : AbstractValidator<AssetAttachmentInput>
{
  public AssetAttachmentInputValidator()
  {
    RuleFor(x => x.AttachmentType).IsInEnum();
    RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(255);
    RuleFor(x => x.StoredFileName).NotEmpty().MaximumLength(255);
    RuleFor(x => x.MimeType).NotEmpty().MaximumLength(100);
    RuleFor(x => x.FileSize).GreaterThanOrEqualTo(0);
    RuleFor(x => x.StoragePath).NotEmpty();
    RuleFor(x => x.ChecksumSha256).Length(64).When(x => x.ChecksumSha256 is not null);
  }
}

public class AddAssetAttachmentCommandValidator : AbstractValidator<AddAssetAttachmentCommand>
{
  public AddAssetAttachmentCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.Attachment).NotNull().SetValidator(new AssetAttachmentInputValidator());
  }
}
