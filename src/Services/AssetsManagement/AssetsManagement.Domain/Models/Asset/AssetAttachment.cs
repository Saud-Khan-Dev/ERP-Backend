public class AssetAttachment : Aggregate<AssetAttachmentId>
{
  public AssetId AssetId { get; private set; } = default!;
  public AttachmentType AttachmentType { get; private set; }
  public string OriginalFileName { get; private set; } = default!;
  public string StoredFileName { get; private set; } = default!;
  public string MimeType { get; private set; } = default!;
  /// Bytes
  public long FileSize { get; private set; }
  /// Object-storage key
  public string StoragePath { get; private set; } = default!;
  /// Integrity / dedupe
  public string? ChecksumSha256 { get; private set; }
  public bool IsPrimaryImage { get; private set; }

  public static AssetAttachment Create(
      AssetAttachmentId id,
      AssetId assetId,
      AttachmentType attachmentType,
      string originalFileName,
      string storedFileName,
      string mimeType,
      long fileSize,
      string storagePath,
      string? checksumSha256,
      bool isPrimaryImage)
  {
    ArgumentNullException.ThrowIfNull(assetId);
    ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
    ArgumentException.ThrowIfNullOrWhiteSpace(storedFileName);
    ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
    ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

    if (fileSize < 0)
      throw new DomainException("File size cannot be negative.");

    if (checksumSha256 is not null && checksumSha256.Length != 64)
      throw new DomainException("checksum_sha256 must be a 64 character hex digest.");

    if (isPrimaryImage && attachmentType != AttachmentType.Image)
      throw new DomainException("Only IMAGE attachments can be the primary image.");

    return new AssetAttachment
    {
      Id = id,
      AssetId = assetId,
      AttachmentType = attachmentType,
      OriginalFileName = originalFileName.Trim(),
      StoredFileName = storedFileName.Trim(),
      MimeType = mimeType.Trim(),
      FileSize = fileSize,
      StoragePath = storagePath.Trim(),
      ChecksumSha256 = checksumSha256?.ToLowerInvariant(),
      IsPrimaryImage = isPrimaryImage
    };
  }

  public void MarkAsPrimary()
  {
    if (AttachmentType != AttachmentType.Image)
      throw new DomainException("Only IMAGE attachments can be the primary image.");

    IsPrimaryImage = true;
  }

  public void UnmarkAsPrimary() => IsPrimaryImage = false;
}
