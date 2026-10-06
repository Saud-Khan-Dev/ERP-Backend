/// The GDA file server. The database keeps only the relative path (file_reference / photo_reference).
public interface IFileStorage
{
  /// Writes the stream under `folder` (a relative path such as /hrm-files/EMP-001/documents) with a server-generated
  /// name, computing size and SHA-256 on the way through.
  Task<StoredFileInfo> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken);

  /// Null when the file is missing from storage.
  Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
}

public sealed record StoredFileInfo(string StoredFileName, string RelativePath, long FileSizeBytes, string ChecksumSha256);
