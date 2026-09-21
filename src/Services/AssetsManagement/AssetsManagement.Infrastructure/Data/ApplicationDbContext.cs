using System.Reflection;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; each service owns a schema.
  public const string Schema = "assets";

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

  // ---- inventory ----
  public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
  public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
  public DbSet<InventoryType> InventoryTypes => Set<InventoryType>();
  public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
  public DbSet<Purchase> Purchases => Set<Purchase>();
  public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
  public DbSet<Warehouse> Warehouses => Set<Warehouse>();
  public DbSet<Scrap> Scraps => Set<Scrap>();

  // ---- taxonomy ----
  public DbSet<AssetClass> AssetClasses => Set<AssetClass>();
  public DbSet<AssetType> AssetTypes => Set<AssetType>();
  public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();

  // ---- dynamic attributes ----
  public DbSet<OptionSet> OptionSets => Set<OptionSet>();
  public DbSet<OptionSetValue> OptionSetValues => Set<OptionSetValue>();
  public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
  public DbSet<AttributeGroup> AttributeGroups => Set<AttributeGroup>();
  public DbSet<AttributeAssignment> AttributeAssignments => Set<AttributeAssignment>();
  public DbSet<AssetAttributeValue> AssetAttributeValues => Set<AssetAttributeValue>();
  public DbSet<AssetAttributeHistory> AssetAttributeHistories => Set<AssetAttributeHistory>();

  // ---- lookups ----
  public DbSet<AssetStatus> AssetStatuses => Set<AssetStatus>();
  public DbSet<CurrencyLookup> Currencies => Set<CurrencyLookup>();
  public DbSet<Location> Locations => Set<Location>();
  public DbSet<LifecycleEventType> LifecycleEventTypes => Set<LifecycleEventType>();
  public DbSet<DepreciationMethod> DepreciationMethods => Set<DepreciationMethod>();
  public DbSet<DisposalMethod> DisposalMethods => Set<DisposalMethod>();

  // ---- core ----
  public DbSet<Asset> Assets => Set<Asset>();
  public DbSet<AssetAcquisition> AssetAcquisitions => Set<AssetAcquisition>();
  public DbSet<AssetAttachment> AssetAttachments => Set<AssetAttachment>();
  public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
  public DbSet<AssetLifecycleEvent> AssetLifecycleEvents => Set<AssetLifecycleEvent>();

  // ---- financial ----
  public DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules => Set<AssetDepreciationSchedule>();
  public DbSet<AssetDepreciationEntry> AssetDepreciationEntries => Set<AssetDepreciationEntry>();
  public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
  public DbSet<AssetDisposal> AssetDisposals => Set<AssetDisposal>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    builder.HasDefaultSchema(Schema);
    builder.HasPostgresExtension("ltree");
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    base.OnModelCreating(builder);
  }
}
