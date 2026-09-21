using Microsoft.EntityFrameworkCore;

public class DeleteAssetAttachmentHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAssetAttachmentCommand, Result<DeleteAssetAttachmentCommandResult>>
{
  public async Task<Result<DeleteAssetAttachmentCommandResult>> Handle(DeleteAssetAttachmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var attachmentId = AssetAttachmentId.Of(command.AttachmentId);

    var attachment = await context.AssetAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAttachmentNotFoundException($"Attachment {command.AttachmentId} was not found on asset {command.AssetId}.");

    // FILE attributes point at attachment ids; refuse to orphan them
    var referencedByAttribute = await context.AssetAttributeValues
      .AnyAsync(v => v.AssetId == assetId && v.ValueText == command.AttachmentId.ToString(), cancellationToken);

    if (referencedByAttribute)
      return Result<DeleteAssetAttachmentCommandResult>.Failure("This attachment is referenced by a FILE attribute of the asset. Clear that attribute first.");

    context.AssetAttachments.Remove(attachment);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAssetAttachmentCommandResult>.Success(new DeleteAssetAttachmentCommandResult(true));
  }
}
