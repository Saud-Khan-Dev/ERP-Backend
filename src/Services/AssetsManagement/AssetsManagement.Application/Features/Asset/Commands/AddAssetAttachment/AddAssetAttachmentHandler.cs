using Microsoft.EntityFrameworkCore;

public class AddAssetAttachmentHandler(IApplicationDbContext context)
  : ICommandHandler<AddAssetAttachmentCommand, Result<AddAssetAttachmentCommandResult>>
{
  public async Task<Result<AddAssetAttachmentCommandResult>> Handle(AddAssetAttachmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    if (!await context.Assets.AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var input = command.Attachment;

    var attachment = AssetAttachment.Create(
      AssetAttachmentId.Of(Guid.NewGuid()), assetId, input.AttachmentType, input.OriginalFileName, input.StoredFileName,
      input.MimeType, input.FileSize, input.StoragePath, input.ChecksumSha256, input.IsPrimaryImage);

    if (input.IsPrimaryImage)
    {
      // only one primary image per asset
      var previous = await context.AssetAttachments.Where(a => a.AssetId == assetId && a.IsPrimaryImage).ToListAsync(cancellationToken);
      previous.ForEach(p => p.UnmarkAsPrimary());
    }

    await context.AssetAttachments.AddAsync(attachment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<AddAssetAttachmentCommandResult>.Success(new AddAssetAttachmentCommandResult(attachment.Id.Value));
  }
}
