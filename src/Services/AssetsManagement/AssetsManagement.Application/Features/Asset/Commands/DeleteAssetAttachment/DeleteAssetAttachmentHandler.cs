using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class DeleteAssetAttachmentHandler(IApplicationDbContext context, IFileStorage storage, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<DeleteAssetAttachmentCommand, Result<DeleteAssetAttachmentCommandResult>>
{
  public async Task<Result<DeleteAssetAttachmentCommandResult>> Handle(DeleteAssetAttachmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var attachmentId = AssetAttachmentId.Of(command.AttachmentId);

    var attachment = await context.AssetAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAttachmentNotFoundException("That file was not found on this asset.");

    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    if (await AssetRecordGuard.ClosedReasonAsync(context, asset, cancellationToken) is { } closed)
      return Result<DeleteAssetAttachmentCommandResult>.Failure(closed);

    // a file field of the asset may point at it; refuse to leave it pointing at nothing
    var referencedByAttribute = await context.AssetAttributeValues
      .AnyAsync(v => v.AssetId == assetId && v.ValueText == command.AttachmentId.ToString(), cancellationToken);

    if (referencedByAttribute)
      return Result<DeleteAssetAttachmentCommandResult>.Failure("One of the asset's details points at this file. Clear that detail first.");

    context.AssetAttachments.Remove(attachment);
    var label = string.IsNullOrWhiteSpace(attachment.Title) ? attachment.OriginalFileName : attachment.Title;
    await SystemEvents.AddAsync(context, assetId, EventPurpose.DocumentRemoved, clock.UtcNow, currentUser.Actor(), $"Removed: {label}",
      JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["File"] = attachment.OriginalFileName, ["Kind"] = attachment.AttachmentType.ToString() }),
      cancellationToken: cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    // the row is gone; the stored file goes too (best effort - a leftover file is harmless)
    try { await storage.DeleteAsync(attachment.StoragePath, cancellationToken); } catch { /* kept on disk */ }

    return Result<DeleteAssetAttachmentCommandResult>.Success(new DeleteAssetAttachmentCommandResult(true));
  }
}
