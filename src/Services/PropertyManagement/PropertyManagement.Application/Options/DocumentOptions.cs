/// Upload limits, bound from the "Documents" configuration section.
public sealed class DocumentOptions
{
  public const string SectionName = "Documents";

  public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

  /// Scans, office documents and images — what GDA files actually contain.
  public string[] AllowedExtensions { get; set; } =
  {
    ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".doc", ".docx", ".xls", ".xlsx"
  };
}
