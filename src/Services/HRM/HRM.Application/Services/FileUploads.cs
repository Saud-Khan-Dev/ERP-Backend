using Microsoft.Extensions.Options;

/// An uploaded file as the API receives it.
public sealed record UploadedFile(Stream Content, string FileName, string? ContentType, long Length);

/// Checks and stores employee papers and photos on the GDA file server under /hrm-files/{employee number}/..., and
/// serves them back. The database keeps only the relative path.
public class FileUploads(IFileStorage storage, IOptions<DocumentOptions> options)
{
  private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
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
    [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
  };

  public Task<string> SaveDocumentAsync(UploadedFile file, string employeeNumber, string area, CancellationToken cancellationToken) =>
      SaveAsync(file, employeeNumber, area, options.Value.AllowedExtensions, options.Value.MaxFileSizeBytes, cancellationToken);

  public Task<string> SavePhotoAsync(UploadedFile file, string employeeNumber, CancellationToken cancellationToken) =>
      SaveAsync(file, employeeNumber, "photo", options.Value.PhotoExtensions, options.Value.MaxPhotoSizeBytes, cancellationToken);

  public async Task<StoredFile> OpenAsync(string relativePath, string downloadName, CancellationToken cancellationToken)
  {
    var stream = await storage.OpenReadAsync(relativePath, cancellationToken)
      ?? throw new StoredFileNotFoundException("The file is missing from the file server. Upload it again.");
    var extension = Path.GetExtension(relativePath);
    return new StoredFile(stream, ContentTypes.GetValueOrDefault(extension, "application/octet-stream"), $"{Safe(downloadName)}{extension}");
  }

  private async Task<string> SaveAsync(UploadedFile file, string employeeNumber, string area, string[] allowed, long maxSize, CancellationToken cancellationToken)
  {
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (file.Length <= 0)
      throw new DomainException("The file is empty.");
    if (file.Length > maxSize)
      throw new DomainException($"The file is larger than the {maxSize / (1024 * 1024)} MB limit.");
    if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
      throw new DomainException($"Files of type '{extension}' are not accepted here. Allowed: {string.Join(", ", allowed)}.");

    var stored = await storage.SaveAsync(file.Content, $"/hrm-files/{Safe(employeeNumber)}/{area}", extension, cancellationToken);
    return stored.RelativePath;
  }

  /// Folder and download names keep letters, digits, '-' and '_' only.
  private static string Safe(string name)
  {
    var cleaned = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
    return string.IsNullOrEmpty(cleaned) ? "file" : cleaned;
  }
}

public sealed record StoredFile(Stream Content, string ContentType, string FileName);
