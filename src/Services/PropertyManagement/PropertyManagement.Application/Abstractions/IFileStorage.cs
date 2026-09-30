/// The GDA file server. The database keeps only metadata and the relative path (schema guide, "Documents").
public interface IFileStorage
{
  /// Writes the stream under `folder` (a relative path such as /property-documents/PROP-00125/notices)
  /// with a server-generated name, computing size and SHA-256 on the way through.
  Task<StoredFileInfo> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken);

  /// Null when the file is missing from storage.
  Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
}

public sealed record StoredFileInfo(string StoredFileName, string RelativePath, long FileSizeBytes, string ChecksumSha256);
