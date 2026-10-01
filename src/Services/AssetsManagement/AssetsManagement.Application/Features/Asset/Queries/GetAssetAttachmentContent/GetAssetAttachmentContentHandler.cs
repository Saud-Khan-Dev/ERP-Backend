using Microsoft.EntityFrameworkCore;

public class GetAssetAttachmentContentHandler(IApplicationDbContext context, IFileStorage storage)
  : IQueryHandler<GetAssetAttachmentContentQuery, Result<AttachmentContent>>
{
  public async Task<Result<AttachmentContent>> Handle(GetAssetAttachmentContentQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    var attachmentId = AssetAttachmentId.Of(query.AttachmentId);
    var attachment = await context.AssetAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAttachmentNotFoundException($"Attachment {query.AttachmentId} was not found on this asset.");

    var stream = await storage.OpenReadAsync(attachment.StoragePath, cancellationToken)
      ?? throw new AssetAttachmentNotFoundException($"The file of '{attachment.OriginalFileName}' is missing from storage.");

    return Result<AttachmentContent>.Success(new AttachmentContent(stream, attachment.MimeType, attachment.OriginalFileName));
  }
}
