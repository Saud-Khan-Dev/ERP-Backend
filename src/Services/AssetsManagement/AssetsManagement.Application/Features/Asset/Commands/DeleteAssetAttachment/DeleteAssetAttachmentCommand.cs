using FluentValidation;

public sealed record DeleteAssetAttachmentCommandResult(bool IsSuccess);

public sealed record DeleteAssetAttachmentCommand(Guid AssetId, Guid AttachmentId) : ICommand<Result<DeleteAssetAttachmentCommandResult>>;

public class DeleteAssetAttachmentCommandValidator : AbstractValidator<DeleteAssetAttachmentCommand>
{
  public DeleteAssetAttachmentCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.AttachmentId).NotEmpty();
  }
}
