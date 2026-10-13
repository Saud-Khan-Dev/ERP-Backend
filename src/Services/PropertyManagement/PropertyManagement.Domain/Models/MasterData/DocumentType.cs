using System.Text.RegularExpressions;

/// document_type.applies_to: whose papers a kind of document is. PROPERTY kinds are filed in a property's file
/// (on the property or one of its records); OWNER kinds are an owner's own papers (CNIC copy, NTN certificate ...)
/// and belong to no property.
public enum DocumentScope { Property, Owner }

/// Notesheet, Ownership Document, Site/Building Plan, Notice/Letter, CNIC Copy ...
/// storage_folder is the sub-folder on the file server: /property-documents/PROP-00125/{storage_folder}/ for a
/// property kind, /owner-documents/OWN-00012/{storage_folder}/ for an owner kind.
public sealed class DocumentType : MasterData
{
  public const int StorageFolderMaxLength = 50;

  private static readonly Regex FolderPattern = new(@"^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public string StorageFolder { get; private set; } = default!;
  public DocumentScope AppliesTo { get; private set; } = DocumentScope.Property;

  public bool AppliesToOwner => AppliesTo == DocumentScope.Owner;

  /// The scope a document attached to this record must have: an owner's papers are OWNER kinds, everything
  /// filed on a property or one of its records a PROPERTY kind.
  public static DocumentScope ScopeOf(DocumentEntityType entityType) =>
      entityType == DocumentEntityType.Owner ? DocumentScope.Owner : DocumentScope.Property;

  public void EnsureAppliesTo(DocumentEntityType entityType)
  {
    if (ScopeOf(entityType) == AppliesTo)
      return;

    throw new DomainException(AppliesToOwner
      ? $"'{Name.Value}' is an owner's document - file it on the owner, not on a property."
      : $"'{Name.Value}' is a property document - it cannot be filed on an owner.");
  }

  protected override void ApplyExtras(MasterExtras extras)
  {
    var folder = extras.StorageFolder ?? StorageFolder ?? Code.Value.ToLowerInvariant().Replace('_', '-');
    folder = folder.Trim().ToLowerInvariant();

    if (folder.Length > StorageFolderMaxLength || !FolderPattern.IsMatch(folder))
      throw new DomainException("storage_folder may only contain lower-case letters, digits, '-' and '_' (e.g. notices).");

    var appliesTo = extras.AppliesTo ?? AppliesTo;
    if (!Enum.IsDefined(appliesTo))
      throw new DomainException("applies_to must be PROPERTY or OWNER.");

    // property_owner.cnic_document_id points at this kind, so it is always an owner's document
    if (Is(SystemMasterCodes.DocumentCnicCopy) && appliesTo != DocumentScope.Owner)
      throw new DomainException($"{SystemMasterCodes.DocumentCnicCopy} is the owner's CNIC copy and always applies to OWNER.");

    StorageFolder = folder;
    AppliesTo = appliesTo;
  }
}
