using Microsoft.EntityFrameworkCore;

/// Load a tracked aggregate by id or throw the matching 404, so handlers read as one line each.
public static class EntityLoaders
{
  public static async Task<Property> LoadPropertyAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(id);
    return await context.Properties.FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {id} was not found.");
  }

  public static async Task<PropertyOwner> LoadOwnerAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var ownerId = OwnerId.Of(id);
    return await context.Owners
        .Include(o => o.Contacts)
        .Include(o => o.Addresses)
        .FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken)
      ?? throw new OwnerNotFoundException($"Owner {id} was not found.");
  }

  public static async Task<PropertyOwnership> LoadOwnershipAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var ownershipId = OwnershipId.Of(id);
    return await context.Ownerships.FirstOrDefaultAsync(o => o.Id == ownershipId, cancellationToken)
      ?? throw new OwnershipNotFoundException($"Ownership {id} was not found.");
  }

  public static async Task<PropertyTransfer> LoadTransferAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var transferId = TransferId.Of(id);
    return await context.Transfers.Include(t => t.Parties).FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken)
      ?? throw new TransferNotFoundException($"Transfer {id} was not found.");
  }

  public static async Task<PropertyEncumbrance> LoadEncumbranceAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var encumbranceId = EncumbranceId.Of(id);
    return await context.Encumbrances.FirstOrDefaultAsync(e => e.Id == encumbranceId, cancellationToken)
      ?? throw new EncumbranceNotFoundException($"Encumbrance {id} was not found.");
  }

  public static async Task<PropertyAreaRegularization> LoadRegularizationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var regularizationId = AreaRegularizationId.Of(id);
    return await context.AreaRegularizations.FirstOrDefaultAsync(r => r.Id == regularizationId, cancellationToken)
      ?? throw new RegularizationNotFoundException($"Area regularization {id} was not found.");
  }

  public static async Task<PropertyDocument> LoadDocumentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var documentId = DocumentId.Of(id);
    return await context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
      ?? throw new DocumentNotFoundException($"Document {id} was not found.");
  }

  public static async Task<AttributeDefinition> LoadAttributeDefinitionAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var definitionId = AttributeDefinitionId.Of(id);
    return await context.AttributeDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
      ?? throw new AttributeDefinitionNotFoundException($"Custom field {id} was not found.");
  }

  /// Current ownership rows of a property (ACTIVE + DISPUTED: a disputed share is still held), tracked so
  /// they can be closed.
  public static Task<List<PropertyOwnership>> CurrentOwnershipsAsync(this IApplicationDbContext context, PropertyId propertyId, CancellationToken cancellationToken) =>
      context.Ownerships
        .Where(o => o.PropertyId == propertyId && (o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Disputed))
        .ToListAsync(cancellationToken);

  /// The property's ownership record as the ownership rules read it: every ownership row (history included,
  /// tracked so rows can be closed), the owners on those rows plus `ownerIds` (the parties of the act), the
  /// waiting transfers with their parties, the charges in force, and today.
  public static async Task<OwnershipRecord> LoadOwnershipRecordAsync(
      this IApplicationDbContext context, Property property, IEnumerable<OwnerId> ownerIds, CancellationToken cancellationToken)
  {
    var ownerships = await context.Ownerships.Where(o => o.PropertyId == property.Id).ToListAsync(cancellationToken);

    var wanted = ownerships.Select(o => o.OwnerId).Concat(ownerIds).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => wanted.Contains(o.Id)).ToListAsync(cancellationToken);

    var pending = await context.Transfers.AsNoTracking().Include(t => t.Parties)
        .Where(t => t.PropertyId == property.Id
          && (t.TransferStatus == TransferStatus.Initiated || t.TransferStatus == TransferStatus.Approved))
        .ToListAsync(cancellationToken);

    var encumbrances = await context.Encumbrances.AsNoTracking()
        .Where(e => e.PropertyId == property.Id && e.Status == EncumbranceStatus.Active)
        .ToListAsync(cancellationToken);

    return new OwnershipRecord(property, ownerships, owners, pending, encumbrances, Today);
  }

  /// The parties of an act must be registered owner records (404 otherwise); returns their ids.
  public static async Task<List<OwnerId>> EnsureOwnersExistAsync(this IApplicationDbContext context, IEnumerable<Guid> ids, CancellationToken cancellationToken)
  {
    var ownerIds = ids.Select(OwnerId.Of).Distinct().ToList();
    var found = await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).Select(o => o.Id).ToListAsync(cancellationToken);

    var missing = ownerIds.Except(found).FirstOrDefault();
    if (missing is not null)
      throw new OwnerNotFoundException($"Owner {missing.Value} was not found.");

    return ownerIds;
  }

  /// The allotment in force on a property (ACTIVE or RESTORED, not withdrawn) other than `except`, with its
  /// allottee's name. At most one may be in force.
  public static async Task<PropertyAllotment.InForce?> AllotmentInForceAsync(
      this IApplicationDbContext context, PropertyId propertyId, AllotmentId? except, CancellationToken cancellationToken)
  {
    var inForceStatuses = await InForceAllotmentStatusIdsAsync(context, cancellationToken);

    var other = (await context.Allotments.AsNoTracking()
        .Where(a => a.PropertyId == propertyId && a.IsActive && inForceStatuses.Contains(a.AllotmentStatusId))
        .OrderByDescending(a => a.AllotmentDate)
        .ToListAsync(cancellationToken))
      .FirstOrDefault(a => a.Id != except);

    if (other is null)
      return null;

    var allottee = await context.Owners.AsNoTracking().FirstOrDefaultAsync(o => o.Id == other.AllotteeOwnerId, cancellationToken);
    return new PropertyAllotment.InForce(other, allottee?.OwnerName.Value ?? "another allottee");
  }

  /// Ids of the allotment statuses that keep an allotment in force (ACTIVE, RESTORED).
  public static async Task<List<MasterId>> InForceAllotmentStatusIdsAsync(this IApplicationDbContext context, CancellationToken cancellationToken) =>
      (await context.Set<AllotmentStatus>().AsNoTracking().ToListAsync(cancellationToken))
        .Where(PropertyAllotment.IsInForceStatus)
        .Select(s => s.Id)
        .ToList();

  /// Today's date (UTC): the "today" every date rule compares against.
  public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
