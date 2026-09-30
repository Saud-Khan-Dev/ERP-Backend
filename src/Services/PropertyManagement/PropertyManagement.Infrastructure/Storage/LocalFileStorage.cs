using System.Security.Cryptography;
using Microsoft.Extensions.Options;

/// The GDA file server as a directory (a mounted share in production). Files are written under
/// RootPath + relative_path with a generated name; the original name is only kept in the database.
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
  private readonly string _root = Path.GetFullPath(options.Value.RootPath);

  public async Task<StoredFileInfo> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken)
  {
    var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    var relativePath = $"{folder.TrimEnd('/')}/{storedFileName}";
    var fullPath = Resolve(relativePath);

    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

    // write to a temp name first so a failed upload never leaves a half file under the real name
    var tempPath = fullPath + ".partial";
    long size;
    string checksum;

    try
    {
      using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      var buffer = new byte[81920];

      await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true))
      {
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
          hash.AppendData(buffer, 0, read);
          await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        size = target.Length;
      }

      checksum = Convert.ToHexStringLower(hash.GetHashAndReset());
      File.Move(tempPath, fullPath);
    }
    catch
    {
      File.Delete(tempPath);
      throw;
    }

    return new StoredFileInfo(storedFileName, relativePath, size, checksum);
  }

  public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
  {
    var fullPath = Resolve(relativePath);

    Stream? stream = File.Exists(fullPath)
      ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
      : null;

    return Task.FromResult(stream);
  }

  /// Keeps every path inside the storage root, whatever the relative path contains.
  private string Resolve(string relativePath)
  {
    var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath.TrimStart('/', '\\')));

    if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
      throw new InvalidOperationException("The document path points outside the storage root.");

    return fullPath;
  }
}

/// Bound from the "FileStorage" configuration section.
public sealed class FileStorageOptions
{
  public const string SectionName = "FileStorage";

  /// Mount point of the GDA file server. Relative paths resolve against the working directory.
  public string RootPath { get; set; } = "storage";
}
