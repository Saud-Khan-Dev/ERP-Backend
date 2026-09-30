using Microsoft.EntityFrameworkCore;

public interface IApplicationDbContext
{
  // ---- property core ----
  DbSet<Property> Properties { get; }
  DbSet<PropertyStatusHistory> PropertyStatusHistories { get; }
  DbSet<PropertyMeasurement> PropertyMeasurements { get; }
  DbSet<PropertyAreaRegularization> AreaRegularizations { get; }

  // ---- owners & ownership ----
  DbSet<PropertyOwner> Owners { get; }
  DbSet<PropertyOwnership> Ownerships { get; }
  DbSet<PropertyTransfer> Transfers { get; }
  DbSet<PropertyEncumbrance> Encumbrances { get; }

  // ---- management ----
  DbSet<PropertyAllotment> Allotments { get; }
  DbSet<PropertyLease> Leases { get; }
  DbSet<PropertyRental> Rentals { get; }
  DbSet<AgreementViolation> Violations { get; }
  DbSet<PropertyAuction> Auctions { get; }
  DbSet<PropertyOutsourcing> Outsourcings { get; }

  // ---- compliance & GIS ----
  DbSet<PropertyBoundary> Boundaries { get; }
  DbSet<PropertyEncroachment> Encroachments { get; }
  DbSet<PropertyLitigation> Litigations { get; }
  DbSet<PropertyAppeal> Appeals { get; }
  DbSet<BuildingPlan> BuildingPlans { get; }

  // ---- documents ----
  DbSet<PropertyDocument> Documents { get; }

  // ---- custom fields ----
  DbSet<AttributeDefinition> AttributeDefinitions { get; }
  DbSet<PropertyAttributeValue> PropertyAttributeValues { get; }

  // ---- numbering ----
  DbSet<CodeSequence> CodeSequences { get; }

  /// The master tables, reached generically through MasterRegistry (27 tables + attribute_group).
  DbSet<TEntity> Set<TEntity>() where TEntity : class;

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
