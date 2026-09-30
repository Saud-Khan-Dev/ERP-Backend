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

  /// Current (ACTIVE) ownership rows of a property, tracked so they can be closed.
  public static Task<List<PropertyOwnership>> CurrentOwnershipsAsync(this IApplicationDbContext context, PropertyId propertyId, CancellationToken cancellationToken) =>
      context.Ownerships
        .Where(o => o.PropertyId == propertyId && o.OwnershipStatus == OwnershipStatus.Active)
        .ToListAsync(cancellationToken);
}
