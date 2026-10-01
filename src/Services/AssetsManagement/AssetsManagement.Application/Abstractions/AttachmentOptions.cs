/// Upload limits for asset documents and photos, bound from the "Attachments" configuration section.
public sealed class AttachmentOptions
{
  public const string SectionName = "Attachments";

  public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

  /// Invoices, warranty cards, manuals (office documents and scans) and photos.
  public string[] AllowedExtensions { get; set; } =
  {
    ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".doc", ".docx", ".xls", ".xlsx", ".txt"
  };
}
