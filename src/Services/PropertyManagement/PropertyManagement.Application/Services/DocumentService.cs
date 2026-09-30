using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// Uploads, versions and resolves documents.
///
/// Rule 10: entity_type + entity_id cannot be a foreign key, so before a row is saved this checks that
/// the record exists and belongs to the same property.
public class DocumentService(
    IApplicationDbContext context,
    IFileStorage storage,
    ICurrentUser currentUser,
    IOptions<DocumentOptions> options)
{
  private const string PropertyRoot = "property-documents";

  public sealed record Upload(Stream Content, string FileName, string? ContentType, long Length);

  /// Where a document goes: its property and the folder its file is stored under.
  public sealed record Target(PropertyId PropertyId, DocumentEntityType EntityType, Guid EntityId, string RootFolder);

  /// property_id is always set (schema guide), so even an owner's own paper — a CNIC copy — is filed in
  /// the file of a property the owner is connected to.
  public async Task<Target> ResolveTargetAsync(PropertyId? propertyId, DocumentEntityType entityType, Guid? entityId, CancellationToken cancellationToken)
  {
    if (propertyId is null)
      throw new DomainException(entityType == DocumentEntityType.Owner
        ? "An owner's document is filed under a property: give the propertyId whose file it belongs to."
        : $"A {entityType} document must belong to a property.");

    var folder = await PropertyFolderAsync(propertyId, cancellationToken);

    if (entityType == DocumentEntityType.Owner)
    {
      var ownerId = OwnerId.Of(entityId ?? Guid.Empty);
      if (!await context.Owners.AnyAsync(o => o.Id == ownerId, cancellationToken))
        throw new OwnerNotFoundException($"Owner {entityId} was not found.");

      return new Target(propertyId, entityType, ownerId.Value, folder);
    }
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
      _ => throw new DomainException($"Unknown document entity type {entityType}.")
    };

    if (!exists)
      throw new DomainException($"{entityType} {id} was not found on this property.");

    return new Target(propertyId, entityType, id, folder);
  }

  public async Task<PropertyDocument> CreateAsync(
      Target target,
      DocumentType documentType,
      Upload upload,
      PropertyDocument.Details details,
      CancellationToken cancellationToken)
  {
    var file = await StoreAsync(target.RootFolder, documentType, upload, cancellationToken);

    var document = PropertyDocument.Create(
      DocumentId.New(), target.PropertyId, documentType, target.EntityType, target.EntityId,
      upload.FileName, file, details, UploadedBy(), DateTime.UtcNow);

    await context.Documents.AddAsync(document, cancellationToken);
    return document;
  }

  public async Task<PropertyDocument> CreateVersionAsync(PropertyDocument current, DocumentType documentType, Upload upload, CancellationToken cancellationToken)
  {
    var folder = await PropertyFolderAsync(current.PropertyId, cancellationToken);

    var file = await StoreAsync(folder, documentType, upload, cancellationToken);
    var version = current.NewVersion(DocumentId.New(), upload.FileName, file, UploadedBy(), DateTime.UtcNow);

    await context.Documents.AddAsync(version, cancellationToken);
    return version;
  }

  /// Confidential documents are only listed or served to callers who may edit property records.
  public bool CanSeeConfidential() => currentUser.HasPermission(PermissionCatalog.Property.Edit);

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

    var stored = await storage.SaveAsync(upload.Content, $"{rootFolder}/{documentType.StorageFolder}", extension, cancellationToken);

    var mimeType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType;
    return new PropertyDocument.StoredFile(stored.StoredFileName, stored.RelativePath, mimeType, stored.FileSizeBytes, stored.ChecksumSha256);
  }

  private async Task<string> PropertyFolderAsync(PropertyId propertyId, CancellationToken cancellationToken)
  {
    var property = await context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {propertyId.Value} was not found.");

    property.EnsureActive();
    return $"/{PropertyRoot}/{property.PropertyCode.Value}";
  }

  private Guid UploadedBy() =>
      currentUser.UserId ?? throw new DomainException("Uploading a document requires a signed-in user.");
}
