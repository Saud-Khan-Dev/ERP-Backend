/// Metadata for one scanned file. The bytes live on the GDA file server at relative_path; the database
/// never stores them.
///
/// A property's papers (entity_type PROPERTY, or one of its records: LEASE, TRANSFER ...) carry property_id, so
/// "all documents of PROP-00125" is one query. An owner's own papers (entity_type OWNER: CNIC copy, NTN
/// certificate ...) belong to the owner alone and have no property_id. entity_type + entity_id say which record
/// it belongs to, and the document type's applies_to must match (PROPERTY kinds on property records, OWNER
/// kinds on owners).
public class PropertyDocument : Aggregate<DocumentId>
{
  /// Null exactly when the document is an owner's own paper (entity_type OWNER).
  public PropertyId? PropertyId { get; private set; }
  public MasterId DocumentTypeId { get; private set; } = default!;
  public DocumentEntityType EntityType { get; private set; }
  /// Id of the record in the table named by entity_type (checked by the application — rule 10).
  public Guid EntityId { get; private set; }
  public string? Title { get; private set; }
  public string OriginalFileName { get; private set; } = default!;
  public string StoredFileName { get; private set; } = default!;
  public string RelativePath { get; private set; } = default!;
  public string MimeType { get; private set; } = default!;
  public long FileSizeBytes { get; private set; }
  public string ChecksumSha256 { get; private set; } = default!;
  /// Date printed on the document itself.
  public DateOnly? DocumentDate { get; private set; }
  /// Notesheet no., letter no. ...
  public string? ReferenceNo { get; private set; }
  public int VersionNo { get; private set; }
  public DocumentId? SupersedesDocumentId { get; private set; }
  public string? Description { get; private set; }
  public bool IsConfidential { get; private set; }
  public bool IsActive { get; private set; }
  public DateTime UploadedAt { get; private set; }
  public Guid UploadedBy { get; private set; }

  /// Where the file ended up once written to storage.
  public sealed record StoredFile(string StoredFileName, string RelativePath, string MimeType, long FileSizeBytes, string ChecksumSha256);

  public sealed record Details(string? Title, DateOnly? DocumentDate, string? ReferenceNo, string? Description, bool IsConfidential);

  public static PropertyDocument Create(
      DocumentId id,
      PropertyId? propertyId,
      DocumentType documentType,
      DocumentEntityType entityType,
      Guid entityId,
      string originalFileName,
      StoredFile file,
      Details details,
      Guid uploadedBy,
      DateTime uploadedAt)
  {
    EnsureCanFile(propertyId, documentType, entityType, entityId);

    if (uploadedBy == Guid.Empty)
      throw new DomainException("The uploading user is required.");

    var document = new PropertyDocument
    {
      Id = id,
      PropertyId = propertyId,
      DocumentTypeId = documentType.Id,
      EntityType = entityType,
      EntityId = entityId,
      VersionNo = 1,
      IsActive = true
    };

    document.SetFile(originalFileName, file, uploadedBy, uploadedAt);
    document.SetDetails(details);
    return document;
  }

  /// The filing rules on their own, so an upload can check them BEFORE the file is written to storage (a refused
  /// upload must not leave an orphan file behind). Create applies them again.
  public static void EnsureCanFile(PropertyId? propertyId, DocumentType documentType, DocumentEntityType entityType, Guid entityId)
  {
    ArgumentNullException.ThrowIfNull(documentType);

    if (!Enum.IsDefined(entityType))
      throw new DomainException("Unknown document entity type.");

    documentType.EnsureActive();
    documentType.EnsureAppliesTo(entityType);

    if (entityType == DocumentEntityType.Owner)
    {
      if (propertyId is not null)
        throw new DomainException("An owner's document is not filed under a property - leave the property empty.");
    }
    else
    {
      if (propertyId is null)
        throw new DomainException($"A {entityType} document must belong to a property.");

      if (entityType == DocumentEntityType.Property && entityId != propertyId.Value)
        throw new DomainException("A PROPERTY document must point at its own property.");
    }

    if (entityId == Guid.Empty)
      throw new DomainException("The document must point at the record it belongs to.");
  }

  /// A paper filed with an ownership history entry follows its step once the entry is registered: to the transfer
  /// the step became, or to the first ownership of the first step. The stored file stays where it is.
  public void MoveToRegisteredRecord(DocumentEntityType entityType, Guid entityId)
  {
    if (EntityType != DocumentEntityType.OwnershipHistory)
      throw new DomainException("Only a paper filed with an ownership history entry moves to the record it became.");
    if (entityType is not (DocumentEntityType.Transfer or DocumentEntityType.Ownership) || entityId == Guid.Empty)
      throw new DomainException("A history paper moves to a transfer or an ownership.");

    EntityType = entityType;
    EntityId = entityId;
  }

  /// A corrected or updated file: a new row with version_no + 1 that supersedes this one. This row stays
  /// as the older version.
  public PropertyDocument NewVersion(DocumentId id, string originalFileName, StoredFile file, Guid uploadedBy, DateTime uploadedAt)
  {
    EnsureActive();

    var version = new PropertyDocument
    {
      Id = id,
      PropertyId = PropertyId,
      DocumentTypeId = DocumentTypeId,
      EntityType = EntityType,
      EntityId = EntityId,
      VersionNo = VersionNo + 1,
      SupersedesDocumentId = Id,
      IsActive = true
    };

    version.SetFile(originalFileName, file, uploadedBy, uploadedAt);
    version.SetDetails(new Details(Title, DocumentDate, ReferenceNo, Description, IsConfidential));
    return version;
  }

  /// Metadata only. The document can be re-typed, but only to another kind of the same scope: an owner's paper
  /// stays an owner's paper and a property paper stays a property paper.
  public void UpdateDetails(DocumentType documentType, Details details)
  {
    ArgumentNullException.ThrowIfNull(documentType);
    EnsureActive();

    if (documentType.Id != DocumentTypeId)
    {
      documentType.EnsureActive();
      documentType.EnsureAppliesTo(EntityType);
    }

    DocumentTypeId = documentType.Id;
    SetDetails(details);
  }

  public bool IsOwnerDocument => EntityType == DocumentEntityType.Owner;

  public void Deactivate() => IsActive = false;
  public void Activate() => IsActive = true;

  private void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException("This document is inactive.");
  }

  private void SetFile(string originalFileName, StoredFile file, Guid uploadedBy, DateTime uploadedAt)
  {
    ArgumentNullException.ThrowIfNull(file);

    if (file.FileSizeBytes <= 0)
      throw new DomainException("The file is empty.");

    if (file.ChecksumSha256.Length != 64)
      throw new DomainException("A SHA-256 checksum is 64 hex characters.");

    OriginalFileName = Guard.RequiredText(Path.GetFileName(originalFileName), 255, "File name");
    StoredFileName = Guard.RequiredText(file.StoredFileName, 255, "Stored file name");
    RelativePath = Guard.RequiredText(file.RelativePath, 500, "Relative path");
    MimeType = Guard.RequiredText(file.MimeType, 100, "MIME type");
    FileSizeBytes = file.FileSizeBytes;
    ChecksumSha256 = file.ChecksumSha256.ToLowerInvariant();
    UploadedBy = uploadedBy;
    UploadedAt = uploadedAt;
  }

  private void SetDetails(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);

    Title = Guard.Text(details.Title, 200, "Title");
    DocumentDate = details.DocumentDate;
    ReferenceNo = Guard.Text(details.ReferenceNo, 100, "Reference no.");
    Description = Guard.Text(details.Description, 4000, "Description");
    IsConfidential = details.IsConfidential;
  }
}
