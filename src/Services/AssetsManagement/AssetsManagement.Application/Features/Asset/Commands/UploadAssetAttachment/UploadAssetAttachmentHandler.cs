using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class UploadAssetAttachmentHandler(IApplicationDbContext context, IFileStorage storage, IOptions<AttachmentOptions> options)
  : ICommandHandler<UploadAssetAttachmentCommand, Result<UploadAssetAttachmentCommandResult>>
{
  public async Task<Result<UploadAssetAttachmentCommandResult>> Handle(UploadAssetAttachmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);

    if (await AssetRecordGuard.ClosedReasonAsync(context, asset, cancellationToken) is { } closed)
      return Result<UploadAssetAttachmentCommandResult>.Failure(closed);

    var settings = options.Value;
    var extension = Path.GetExtension(command.FileName).ToLowerInvariant();

    if (command.Length <= 0)
      return Result<UploadAssetAttachmentCommandResult>.Failure("The file is empty.");

    if (command.Length > settings.MaxFileSizeBytes)
      return Result<UploadAssetAttachmentCommandResult>.Failure($"The file is larger than the {settings.MaxFileSizeBytes / (1024 * 1024)} MB limit.");

    if (!settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
      return Result<UploadAssetAttachmentCommandResult>.Failure($"Files of this kind ({(extension == "" ? "no extension" : extension)}) are not accepted. Upload a PDF, a picture, a Word or Excel file, or a text file.");

    // the type a file is served with comes from its extension (from the allow-list), never from the browser's claim
    var mimeType = MimeTypes.GetValueOrDefault(extension, "application/octet-stream");
    var isImage = mimeType.StartsWith("image/", StringComparison.Ordinal);
    var type = command.AttachmentType ?? (isImage ? AttachmentType.Image : AttachmentType.Document);

    if (type == AttachmentType.Image && !isImage)
      return Result<UploadAssetAttachmentCommandResult>.Failure("Only a picture file (JPG, PNG, WEBP or TIFF) can be added as a photo.");

    if (command.IsPrimaryImage && type != AttachmentType.Image)
      return Result<UploadAssetAttachmentCommandResult>.Failure("Only a photo can be the asset's main picture.");

    var stored = await storage.SaveAsync(command.Content, $"/asset-files/{asset.AssetCode.Value}", extension, cancellationToken);

    var attachment = AssetAttachment.Create(
      AssetAttachmentId.Of(Guid.NewGuid()), assetId, type, Path.GetFileName(command.FileName), stored.StoredFileName,
      mimeType, stored.FileSizeBytes, stored.RelativePath, stored.ChecksumSha256, command.IsPrimaryImage, command.Title);

    // the first photo becomes the main picture unless one is chosen; only one main picture per asset
    var primaries = await context.AssetAttachments.Where(a => a.AssetId == assetId && a.IsPrimaryImage).ToListAsync(cancellationToken);
    if (command.IsPrimaryImage)
      primaries.ForEach(p => p.UnmarkAsPrimary());
    else if (type == AttachmentType.Image && primaries.Count == 0)
      attachment.MarkAsPrimary();

    await context.AssetAttachments.AddAsync(attachment, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UploadAssetAttachmentCommandResult>.Success(new UploadAssetAttachmentCommandResult(attachment.Id.Value, attachment.OriginalFileName, attachment.FileSize));
  }

  private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
  {
    [".pdf"] = "application/pdf",
    [".jpg"] = "image/jpeg",
    [".jpeg"] = "image/jpeg",
    [".png"] = "image/png",
    [".webp"] = "image/webp",
    [".tif"] = "image/tiff",
    [".tiff"] = "image/tiff",
    [".doc"] = "application/msword",
    [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    [".xls"] = "application/vnd.ms-excel",
    [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    [".txt"] = "text/plain",
  };
}
