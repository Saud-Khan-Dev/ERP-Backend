using Microsoft.EntityFrameworkCore;

public interface IApplicationDbContext
{
  // ---- inventory ----
  DbSet<InventoryCategory> InventoryCategories { get; }
  DbSet<InventoryItem> InventoryItems { get; }
  DbSet<InventoryType> InventoryTypes { get; }
  DbSet<InventoryStock> InventoryStocks { get; }
  DbSet<Purchase> Purchases { get; }
  DbSet<PurchaseLine> PurchaseLines { get; }
  DbSet<Warehouse> Warehouses { get; }
  DbSet<Scrap> Scraps { get; }

  // ---- taxonomy ----
  DbSet<AssetClass> AssetClasses { get; }
  DbSet<AssetType> AssetTypes { get; }
  DbSet<AssetCategory> AssetCategories { get; }

  // ---- dynamic attributes ----
  DbSet<OptionSet> OptionSets { get; }
  DbSet<OptionSetValue> OptionSetValues { get; }
  DbSet<AttributeDefinition> AttributeDefinitions { get; }
  DbSet<AttributeGroup> AttributeGroups { get; }
  DbSet<AttributeAssignment> AttributeAssignments { get; }
  DbSet<AssetAttributeValue> AssetAttributeValues { get; }
  DbSet<AssetAttributeHistory> AssetAttributeHistories { get; }

  // ---- lookups ----
  DbSet<AssetStatus> AssetStatuses { get; }
  DbSet<CurrencyLookup> Currencies { get; }
  DbSet<Location> Locations { get; }
  DbSet<LifecycleEventType> LifecycleEventTypes { get; }
  DbSet<DepreciationMethod> DepreciationMethods { get; }
  DbSet<DisposalMethod> DisposalMethods { get; }

  // ---- core ----
  DbSet<Asset> Assets { get; }
  DbSet<AssetAcquisition> AssetAcquisitions { get; }
  DbSet<AssetAttachment> AssetAttachments { get; }
  DbSet<AssetAssignment> AssetAssignments { get; }
  DbSet<AssetLifecycleEvent> AssetLifecycleEvents { get; }

  // ---- financial ----
  DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules { get; }
  DbSet<AssetDepreciationEntry> AssetDepreciationEntries { get; }
  DbSet<AssetValuation> AssetValuations { get; }
  DbSet<AssetDisposal> AssetDisposals { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
