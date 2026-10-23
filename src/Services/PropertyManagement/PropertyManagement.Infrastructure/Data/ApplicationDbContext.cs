using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; this service owns the "property" schema.
  public const string Schema = "property";

  /// The name of the query filters that narrow every property, and every record on one, to the person's towns.
  public const string TownScopeFilter = "TownScope";

  private readonly IReadOnlySet<Guid>? _townScope;
  // read by the query filters, per context instance (EF parameterises them): whether a scope applies, and its towns
  private readonly bool _scoped;
  private readonly List<MasterId> _towns;

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser? currentUser = null) : base(options)
  {
    _townScope = currentUser?.TownScope;
    _scoped = _townScope is not null;
    _towns = (_townScope ?? new HashSet<Guid>()).Select(MasterId.Of).ToList();
  }

  public IReadOnlySet<Guid>? TownScope => _townScope;

  public IQueryable<OwnershipLink> OwnershipLinks => Set<OwnershipLink>().AsNoTracking();

  public DbSet<AuditRecord> AuditTrail => Set<AuditRecord>();

  // ---- property core ----
  public DbSet<Property> Properties => Set<Property>();
  public DbSet<PropertyStatusHistory> PropertyStatusHistories => Set<PropertyStatusHistory>();
  public DbSet<PropertyMeasurement> PropertyMeasurements => Set<PropertyMeasurement>();
  public DbSet<PropertyAreaRegularization> AreaRegularizations => Set<PropertyAreaRegularization>();

  // ---- owners & ownership ----
  public DbSet<PropertyOwner> Owners => Set<PropertyOwner>();
  public DbSet<PropertyOwnership> Ownerships => Set<PropertyOwnership>();
  public DbSet<PropertyTransfer> Transfers => Set<PropertyTransfer>();
  public DbSet<PropertyEncumbrance> Encumbrances => Set<PropertyEncumbrance>();

  // ---- management ----
  public DbSet<PropertyAllotment> Allotments => Set<PropertyAllotment>();
  public DbSet<PropertyLease> Leases => Set<PropertyLease>();
  public DbSet<PropertyRental> Rentals => Set<PropertyRental>();
  public DbSet<AgreementViolation> Violations => Set<AgreementViolation>();
  public DbSet<PropertyAuction> Auctions => Set<PropertyAuction>();
  public DbSet<PropertyOutsourcing> Outsourcings => Set<PropertyOutsourcing>();

  // ---- compliance & GIS ----
  public DbSet<PropertyBoundary> Boundaries => Set<PropertyBoundary>();
  public DbSet<PropertyEncroachment> Encroachments => Set<PropertyEncroachment>();
  public DbSet<PropertyLitigation> Litigations => Set<PropertyLitigation>();
  public DbSet<PropertyAppeal> Appeals => Set<PropertyAppeal>();
  public DbSet<BuildingPlan> BuildingPlans => Set<BuildingPlan>();

  // ---- documents ----
  public DbSet<PropertyDocument> Documents => Set<PropertyDocument>();

  // ---- custom fields ----
  public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
  public DbSet<PropertyAttributeValue> PropertyAttributeValues => Set<PropertyAttributeValue>();

  // ---- numbering ----
  public DbSet<CodeSequence> CodeSequences => Set<CodeSequence>();

  // ---- change log ----
  public DbSet<PropertyChange> PropertyChanges => Set<PropertyChange>();

  public void Touch(object entity)
  {
    var entry = Entry(entity);

    // the audit interceptor then stamps updated_at / updated_by, and the UPDATE carries "WHERE xmin = <as read>"
    if (entry.State == EntityState.Unchanged && entry.Metadata.FindProperty(nameof(IEntity.UpdatedAt)) is not null)
      entry.Property(nameof(IEntity.UpdatedAt)).IsModified = true;
  }

  public async Task<Dictionary<Guid, int>> ReferenceCountsAsync(Type principal, IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
  {
    var entity = Model.FindEntityType(principal) ?? throw new InvalidOperationException($"{principal.Name} is not in the model.");
    var counts = new Dictionary<Guid, int>();
    var connection = Database.GetDbConnection();
    var opened = connection.State != ConnectionState.Open;
    if (opened)
      await connection.OpenAsync(cancellationToken);

    try
    {
      foreach (var foreignKey in entity.GetReferencingForeignKeys())
      {
        var dependent = foreignKey.DeclaringEntityType;
        var table = dependent.GetTableName();
        if (table is null)
          continue;

        var schema = dependent.GetSchema() ?? Schema;
        var column = foreignKey.Properties[0].GetColumnName(StoreObjectIdentifier.Table(table, schema));
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"{column}\", count(*)::int FROM \"{schema}\".\"{table}\" WHERE \"{column}\" IS NOT NULL"
          + (ids is { Count: > 0 } ? $" AND \"{column}\" = ANY(@ids)" : string.Empty) + $" GROUP BY \"{column}\"";
        if (ids is { Count: > 0 })
        {
          var parameter = command.CreateParameter();
          parameter.ParameterName = "ids";
          parameter.Value = ids.ToArray();
          command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
          var id = reader.GetGuid(0);
          counts[id] = counts.GetValueOrDefault(id) + reader.GetInt32(1);
        }
      }
    }
    finally
    {
      if (opened)
        await connection.CloseAsync();
    }

    return counts;
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    builder.HasDefaultSchema(Schema);
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    // one table per master, all sharing MasterDataConfiguration<T>
    foreach (var descriptor in MasterRegistry.All)
      MasterDataConfiguration.Apply(builder, descriptor);

    // the shared audit trail (audit_entry): append-only, kept forever
    builder.AddAuditTrail();

    // owner <-> property links that no town scope narrows (a view over the ownership table; no table of its own)
    builder.Entity<OwnershipLink>(b =>
    {
      b.HasNoKey();
      b.ToView("property_ownership", Schema);
      b.Property(x => x.OwnerId).HasConversion(id => id.Value, value => OwnerId.Of(value)).HasColumnName("owner_id");
      b.Property(x => x.PropertyId).HasConversion(id => id.Value, value => PropertyId.Of(value)).HasColumnName("property_id");
    });

    ApplyTownScope(builder);

    base.OnModelCreating(builder);
  }

  /// A person with a town scope sees and acts only on the properties in those towns and on everything recorded on them:
  /// every query of these entities is narrowed (named filter TownScopeFilter), so lists, counts, reports, the bell and
  /// detail reads (404) follow it without each handler repeating it. Owners are not filtered here - a party's name on a
  /// record in scope must always show; the owner screens apply OwnerScope. Checks that must see every town call
  /// IgnoreQueryFilters().
  private void ApplyTownScope(ModelBuilder builder)
  {
    builder.Entity<Property>().HasQueryFilter(TownScopeFilter, p => !_scoped || _towns.Contains(p.TownId));

    // a record on a property: in scope when its property is
    builder.Entity<PropertyStatusHistory>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyMeasurement>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyAreaRegularization>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyOwnership>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyTransfer>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyEncumbrance>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyAllotment>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyLease>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyRental>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<AgreementViolation>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyAuction>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyOutsourcing>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyBoundary>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyEncroachment>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyLitigation>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyAppeal>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<BuildingPlan>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    builder.Entity<PropertyAttributeValue>().HasQueryFilter(TownScopeFilter, x => !_scoped || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
    // the parts of a record follow the record (always read through it; the filters keep EF's view of them consistent)
    builder.Entity<PropertyTransferParty>().HasQueryFilter(TownScopeFilter, x => !_scoped
      || Set<PropertyTransfer>().Any(t => t.Id == x.PropertyTransferId && Set<Property>().Any(p => p.Id == t.PropertyId && _towns.Contains(p.TownId))));
    builder.Entity<AuctionBid>().HasQueryFilter(TownScopeFilter, x => !_scoped
      || Set<PropertyAuction>().Any(a => a.Id == x.AuctionId && Set<Property>().Any(p => p.Id == a.PropertyId && _towns.Contains(p.TownId))));
    builder.Entity<LitigationParty>().HasQueryFilter(TownScopeFilter, x => !_scoped
      || Set<PropertyLitigation>().Any(l => l.Id == x.LitigationId && Set<Property>().Any(p => p.Id == l.PropertyId && _towns.Contains(p.TownId))));
    builder.Entity<LitigationHearing>().HasQueryFilter(TownScopeFilter, x => !_scoped
      || Set<PropertyLitigation>().Any(l => l.Id == x.LitigationId && Set<Property>().Any(p => p.Id == l.PropertyId && _towns.Contains(p.TownId))));
    // an owner's own paper has no property: the owner screens decide whether its owner is visible (OwnerScope)
    builder.Entity<PropertyDocument>().HasQueryFilter(TownScopeFilter,
      x => !_scoped || x.PropertyId == null || Set<Property>().Any(p => p.Id == x.PropertyId && _towns.Contains(p.TownId)));
  }
}
