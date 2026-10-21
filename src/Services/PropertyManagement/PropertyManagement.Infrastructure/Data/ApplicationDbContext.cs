using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; this service owns the "property" schema.
  public const string Schema = "property";

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

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

    base.OnModelCreating(builder);
  }
}
