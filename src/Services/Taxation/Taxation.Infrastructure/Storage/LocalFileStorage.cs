using Microsoft.Extensions.Options;

public sealed class FileStorageOptions
{
  public const string SectionName = "FileStorage";

  public string RootPath { get; set; } = "storage";
}

/// The GDA file server as a directory (a mounted share in production). Files are written under RootPath + relative path
/// with a generated name; the database keeps the relative path.
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
  private readonly string _root = Path.GetFullPath(options.Value.RootPath);

  public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken)
  {
    var relativePath = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    var fullPath = Resolve(relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

    // write to a temp name first so a failed upload never leaves a half file under the real name
    var tempPath = fullPath + ".partial";
    try
    {
      await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await content.CopyToAsync(target, cancellationToken);
      File.Move(tempPath, fullPath);
    }
    catch
    {
      File.Delete(tempPath);
      throw;
    }
    return relativePath;
  }

  public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
  {
    var fullPath = Resolve(relativePath);
    Stream? stream = File.Exists(fullPath) ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    return Task.FromResult(stream);
  }

  /// A relative path inside the root; anything that would leave it is refused.
  private string Resolve(string relativePath)
  {
    var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath.TrimStart('/', '\\')));
    if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
      throw new InvalidOperationException("The file path leaves the storage root.");
    return fullPath;
  }
}
