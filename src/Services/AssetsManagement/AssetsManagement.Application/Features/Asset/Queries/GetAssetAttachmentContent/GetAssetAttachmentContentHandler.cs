using Microsoft.EntityFrameworkCore;

public class GetAssetAttachmentContentHandler(IApplicationDbContext context, IFileStorage storage, IAuditTrail audit)
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

    if (query.Download && await context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken) is { } asset)
    {
      audit.Line(AuditActions.Downloaded, AssetAuditKinds.Document, asset, attachment.Id.Value,
        recordLabel: string.IsNullOrWhiteSpace(attachment.Title) ? attachment.OriginalFileName : attachment.Title, detail: attachment.OriginalFileName);
      await audit.SaveAsync(cancellationToken);
    }

    return Result<AttachmentContent>.Success(new AttachmentContent(stream, attachment.MimeType, attachment.OriginalFileName));
  }
}
