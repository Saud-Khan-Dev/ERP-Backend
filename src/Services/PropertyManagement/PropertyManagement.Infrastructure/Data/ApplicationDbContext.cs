using System.Reflection;
using Microsoft.EntityFrameworkCore;

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

  // ---- documents ----
  public DbSet<PropertyDocument> Documents => Set<PropertyDocument>();

  // ---- custom fields ----
  public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
  public DbSet<PropertyAttributeValue> PropertyAttributeValues => Set<PropertyAttributeValue>();

  // ---- numbering ----
  public DbSet<CodeSequence> CodeSequences => Set<CodeSequence>();

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
