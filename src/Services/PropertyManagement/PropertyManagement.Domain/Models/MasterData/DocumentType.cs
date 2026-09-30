using System.Text.RegularExpressions;

/// Notesheet, Ownership Document, Site/Building Plan, Notice/Letter, CNIC Copy ...
/// storage_folder is the sub-folder on the file server: /property-documents/PROP-00125/{storage_folder}/.
public sealed class DocumentType : MasterData
{
  public const int StorageFolderMaxLength = 50;

  private static readonly Regex FolderPattern = new(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public string StorageFolder { get; private set; } = default!;

  protected override void ApplyExtras(MasterExtras extras)
  {
    var folder = extras.StorageFolder ?? StorageFolder ?? Code.Value.ToLowerInvariant().Replace('_', '-');
    folder = folder.Trim().ToLowerInvariant();

    if (folder.Length > StorageFolderMaxLength || !FolderPattern.IsMatch(folder))
      throw new DomainException("storage_folder may only contain lower-case letters, digits, '-' and '_' (e.g. notices).");

    StorageFolder = folder;
  }
}
