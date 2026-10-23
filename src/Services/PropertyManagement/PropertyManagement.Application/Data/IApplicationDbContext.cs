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

  // ---- change log ----
  DbSet<PropertyChange> PropertyChanges { get; }

  // ---- numbering ----
  DbSet<CodeSequence> CodeSequences { get; }

  // ---- audit trail (audit_entry, append-only) ----
  DbSet<AuditRecord> AuditTrail { get; }

  // ---- town scope ----
  /// The towns the signed-in person works on (ICurrentUser.TownScope); null = every town. Every query of a property and
  /// of a record on a property is narrowed to them by the model's query filters (named TownScopeFilter.Name); a check
  /// that must see every town (a code in use, an owner's commitments) calls IgnoreQueryFilters().
  IReadOnlySet<Guid>? TownScope { get; }

  /// Owner ↔ property links, never narrowed by the town scope (see OwnershipLink).
  IQueryable<OwnershipLink> OwnershipLinks { get; }

  /// The master tables, reached generically through MasterRegistry (27 tables + attribute_group).
  DbSet<TEntity> Set<TEntity>() where TEntity : class;

  /// Marks a tracked row as changed even when no column of it changed, so the save checks its row version (xmin):
  /// an act that changes ownership or allotments touches the property, and a second act on the same property that
  /// read it before the first one saved fails with a concurrency conflict (409) instead of both going through.
  void Touch(object entity);

  /// How many rows of the other tables point at each row of `principal` (a master table), through the foreign keys of
  /// the model: id -> count, rows nobody refers to left out. `ids` narrows it to those rows. A value nothing refers to
  /// can be deleted; a measurement unit any area uses keeps its factor.
  Task<Dictionary<Guid, int>> ReferenceCountsAsync(Type principal, IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken);

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
