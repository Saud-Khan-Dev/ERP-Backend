using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// Uploads, versions and resolves documents - one pipeline for a property's papers and an owner's own papers.
///
/// A property's papers are filed under /property-documents/{PROP code}/{type folder}/ with property_id set; rule 10:
/// entity_type + entity_id cannot be a foreign key, so before a row is saved this checks that the record exists
/// and belongs to the same property. An owner's papers (OWNER document types) are filed under
/// /owner-documents/{OWN code}/{type folder}/ with no property.
///
/// Every check runs BEFORE the file is written, so a refused upload never leaves an orphan file in storage.
public class DocumentService(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser,
    IOptions<DocumentOptions> options)
{
  private const string PropertyRoot = "property-documents";
  private const string OwnerRoot = "owner-documents";

  public sealed record Upload(Stream Content, string FileName, string? ContentType, long Length);

  /// Where a document goes: its property (none for an owner's paper), the record it belongs to and the folder
  /// its file is stored under.
  public sealed record Target(PropertyId? PropertyId, DocumentEntityType EntityType, Guid EntityId, string RootFolder);

  /// A paper filed in a property's file: on the property itself or on one of its records. Owner papers are
  /// filed on the owner (OwnerTargetAsync).
  public async Task<Target> PropertyTargetAsync(PropertyId propertyId, DocumentEntityType entityType, Guid? entityId, CancellationToken cancellationToken)
  {
    if (entityType == DocumentEntityType.Owner)
      throw new DomainException("An owner's paper is filed on the owner (Owners > Documents), not on a property.");
    EnsureMayFileOn(entityType);

    var folder = await PropertyFolderAsync(propertyId, cancellationToken);
    var id = entityType == DocumentEntityType.Property ? propertyId.Value : entityId ?? Guid.Empty;

    if (id == Guid.Empty)
      throw new DomainException($"entityId is required for a {entityType} document.");

    var exists = entityType switch
    {
      DocumentEntityType.Property => true,
      DocumentEntityType.Ownership => await context.Ownerships.AnyAsync(o => o.Id == OwnershipId.Of(id) && o.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Transfer => await context.Transfers.AnyAsync(t => t.Id == TransferId.Of(id) && t.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Encumbrance => await context.Encumbrances.AnyAsync(e => e.Id == EncumbranceId.Of(id) && e.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Regularization => await context.AreaRegularizations.AnyAsync(r => r.Id == AreaRegularizationId.Of(id) && r.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Allotment => await context.Allotments.AnyAsync(x => x.Id == AllotmentId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Lease => await context.Leases.AnyAsync(x => x.Id == LeaseId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Rental => await context.Rentals.AnyAsync(x => x.Id == RentalId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Violation => await context.Violations.AnyAsync(x => x.Id == ViolationId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Auction => await context.Auctions.AnyAsync(x => x.Id == AuctionId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Outsourcing => await context.Outsourcings.AnyAsync(x => x.Id == OutsourcingId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Encroachment => await context.Encroachments.AnyAsync(x => x.Id == EncroachmentId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Litigation => await context.Litigations.AnyAsync(x => x.Id == LitigationId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.Appeal => await context.Appeals.AnyAsync(x => x.Id == AppealId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.BuildingPlan => await context.BuildingPlans.AnyAsync(x => x.Id == BuildingPlanId.Of(id) && x.PropertyId == propertyId, cancellationToken),
      DocumentEntityType.OwnershipHistory => await context.HistoryEntries.AnyAsync(x => x.Id == HistoryEntryId.Of(id) && x.PropertyId == propertyId && x.Status == HistoryEntryStatus.Waiting, cancellationToken),
      _ => throw new DomainException($"Unknown document entity type {entityType}.")
    };

    if (!exists)
      throw new DomainException($"{entityType} {id} was not found on this property.");

    return new Target(propertyId, entityType, id, folder);
  }

  /// An owner's own paper: the owner must exist and be active.
  public async Task<Target> OwnerTargetAsync(OwnerId ownerId, CancellationToken cancellationToken)
  {
    await context.EnsureOwnerVisibleAsync(ownerId, cancellationToken);
    return new(null, DocumentEntityType.Owner, ownerId.Value, await OwnerFolderAsync(ownerId, cancellationToken));
  }

  public async Task<PropertyDocument> CreateAsync(
      Target target,
      DocumentType documentType,
      Upload upload,
      PropertyDocument.Details details,
      CancellationToken cancellationToken)
  {
    // refuse before anything is written to storage
    PropertyDocument.EnsureCanFile(target.PropertyId, documentType, target.EntityType, target.EntityId);
    var file = await StoreAsync(target.RootFolder, documentType, upload, cancellationToken);

    var document = PropertyDocument.Create(
      DocumentId.New(), target.PropertyId, documentType, target.EntityType, target.EntityId,
      upload.FileName, file, details, UploadedBy(), DateTime.UtcNow);

    await context.Documents.AddAsync(document, cancellationToken);
    return document;
  }

  public async Task<PropertyDocument> CreateVersionAsync(PropertyDocument current, DocumentType documentType, Upload upload, CancellationToken cancellationToken)
  {
    if (!current.IsActive)
      throw new DomainException("This document is inactive.");

    var folder = current.PropertyId is { } propertyId
      ? await PropertyFolderAsync(propertyId, cancellationToken)
      : await OwnerFolderAsync(OwnerId.Of(current.EntityId), cancellationToken);

    var file = await StoreAsync(folder, documentType, upload, cancellationToken);
    var version = current.NewVersion(DocumentId.New(), upload.FileName, file, UploadedBy(), DateTime.UtcNow);

    await context.Documents.AddAsync(version, cancellationToken);
    return version;
  }

  /// Keeps property_owner.cnic_document_id on the owner's current CNIC copy after one of the owner's documents was
  /// uploaded, versioned, (de)activated or re-typed. Call it after the change and before saving: it reads the
  /// owner's documents as tracked (so unsaved changes and new rows count). A property document is ignored.
  public async Task RefreshOwnerCnicAsync(PropertyDocument changed, CancellationToken cancellationToken)
  {
    if (!changed.IsOwnerDocument)
      return;

    var ownerId = OwnerId.Of(changed.EntityId);
    var owner = await context.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken);
    if (owner is null)
      return;

    var cnicCode = MasterCode.Of(SystemMasterCodes.DocumentCnicCopy);
    var cnicCopy = await context.Set<DocumentType>().AsNoTracking()
        .FirstOrDefaultAsync(t => t.Code == cnicCode, cancellationToken)
      ?? throw new DomainException($"Document type {SystemMasterCodes.DocumentCnicCopy} is missing; restart the service so it is seeded again.");

    // tracked on purpose: rows already loaded keep their unsaved changes, and Local adds the new ones
    await context.Documents.Where(d => d.EntityType == DocumentEntityType.Owner && d.EntityId == ownerId.Value).LoadAsync(cancellationToken);
    var documents = context.Documents.Local.Where(d => d.EntityType == DocumentEntityType.Owner && d.EntityId == ownerId.Value).ToList();

    owner.RefreshCnicDocument(documents, cnicCopy);
  }

  /// The records whose papers are legal matters: a paper filed on an encroachment, a court case or an appeal.
  public static readonly DocumentEntityType[] LegalRecordKinds = [DocumentEntityType.Encroachment, DocumentEntityType.Litigation, DocumentEntityType.Appeal];

  /// Confidential papers and papers filed on a legal record are listed and served only to callers who may see legal
  /// matters (PROPERTY_LEGAL.VIEW); everyone else sees how many were held back ("Restricted").
  public bool CanSeeConfidential() => currentUser.HasPermission(PermissionCatalog.PropertyLegal.View);

  /// Whether the caller may see this paper at all (see CanSeeConfidential).
  public bool MaySee(PropertyDocument document) =>
      CanSeeConfidential() || (!document.IsConfidential && !LegalRecordKinds.Contains(document.EntityType));

  /// Refuses (403) a paper the caller may not see: reading, downloading or changing it.
  public void EnsureMaySee(PropertyDocument document)
  {
    if (!MaySee(document))
      throw new PermissionRequiredException(document.IsConfidential
        ? "This paper is confidential; it needs Legal: view (PROPERTY_LEGAL.VIEW)."
        : "This paper is filed on a legal record; it needs Legal: view (PROPERTY_LEGAL.VIEW).");
  }

  /// Filing a paper on an encroachment, case or appeal is legal work (PROPERTY_LEGAL.EDIT, 403 otherwise).
  public void EnsureMayFileOn(DocumentEntityType entityType)
  {
    if (LegalRecordKinds.Contains(entityType) && !currentUser.HasPermission(PermissionCatalog.PropertyLegal.Edit))
      throw new PermissionRequiredException("Filing a paper on an encroachment, court case or appeal needs Legal: edit (PROPERTY_LEGAL.EDIT).");
  }

  private async Task<PropertyDocument.StoredFile> StoreAsync(string rootFolder, DocumentType documentType, Upload upload, CancellationToken cancellationToken)
  {
    var settings = options.Value;
    var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();

    if (upload.Length <= 0)
      throw new DomainException("The uploaded file is empty.");

    if (upload.Length > settings.MaxFileSizeBytes)
      throw new DomainException($"The file is larger than the {settings.MaxFileSizeBytes / (1024 * 1024)} MB limit.");

    if (!settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
      throw new DomainException($"Files of type '{extension}' are not accepted. Allowed: {string.Join(", ", settings.AllowedExtensions)}.");

    // the uploading user is checked before the file is written, too
    UploadedBy();

    var stored = await storage.SaveAsync(upload.Content, $"{rootFolder}/{documentType.StorageFolder}", extension, cancellationToken);

    // the type is the file name's, never what the uploader's browser claimed (a .pdf sent as text/html stays a PDF)
    var mimeType = SafeFiles.ContentTypeOf(upload.FileName);
    return new PropertyDocument.StoredFile(stored.StoredFileName, stored.RelativePath, mimeType, stored.FileSizeBytes, stored.ChecksumSha256);
  }

  private async Task<string> PropertyFolderAsync(PropertyId propertyId, CancellationToken cancellationToken)
  {
    var property = await context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {propertyId.Value} was not found.");

    property.EnsureActive();
    return $"/{PropertyRoot}/{property.PropertyCode.Value}";
  }

  private async Task<string> OwnerFolderAsync(OwnerId ownerId, CancellationToken cancellationToken)
  {
    var owner = await context.Owners.AsNoTracking().FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken)
      ?? throw new OwnerNotFoundException($"Owner {ownerId.Value} was not found.");

    if (!owner.IsActive)
      throw new DomainException($"Owner {owner.OwnerCode.Value} is inactive. Reactivate the owner before filing documents.");

    return $"/{OwnerRoot}/{owner.OwnerCode.Value}";
  }

  private Guid UploadedBy() =>
      currentUser.UserId ?? throw new DomainException("Uploading a document requires a signed-in user.");
}
