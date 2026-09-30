using Microsoft.EntityFrameworkCore;

/// Every master (lookup) table, keyed by the slug used in /masters/{type}.
///
/// This is what makes master data "dynamic": one generic API and one generic set of handlers serve all
/// of them, and adding a master is one line here plus a class in the domain — no new endpoints.
public static class MasterRegistry
{
  public static IReadOnlyList<MasterDescriptor> All { get; } = new[]
  {
    // ---- property ----
    MasterDescriptor.Of<Town>("towns", "town", "Town"),
    MasterDescriptor.Of<PropertyType>("property-types", "property_type", "Property type"),
    MasterDescriptor.Of<PropertyStatus>("property-statuses", "property_status", "Property status"),
    MasterDescriptor.Of<PropertyClassification>("property-classifications", "property_classification", "Property classification"),
    MasterDescriptor.Of<MeasurementUnit>("measurement-units", "measurement_unit", "Measurement unit", MasterExtraColumns.FactorToBase | MasterExtraColumns.IsBase),

    // ---- owners & ownership ----
    MasterDescriptor.Of<OwnerType>("owner-types", "owner_type", "Owner type"),
    MasterDescriptor.Of<TenureType>("tenure-types", "tenure_type", "Tenure type"),
    MasterDescriptor.Of<TransferType>("transfer-types", "transfer_type", "Transfer type", MasterExtraColumns.RequiresRelationship),
    MasterDescriptor.Of<ContactType>("contact-types", "contact_type", "Contact type"),
    MasterDescriptor.Of<EncumbranceType>("encumbrance-types", "encumbrance_type", "Encumbrance type"),

    // ---- management ----
    MasterDescriptor.Of<AllotmentType>("allotment-types", "allotment_type", "Allotment type"),
    MasterDescriptor.Of<AllotmentStatus>("allotment-statuses", "allotment_status", "Allotment status"),
    MasterDescriptor.Of<LeaseType>("lease-types", "lease_type", "Lease type"),
    MasterDescriptor.Of<LeaseStatus>("lease-statuses", "lease_status", "Lease status"),
    MasterDescriptor.Of<RentalType>("rental-types", "rental_type", "Rental type"),
    MasterDescriptor.Of<RentalStatus>("rental-statuses", "rental_status", "Rental status"),
    MasterDescriptor.Of<AuctionType>("auction-types", "auction_type", "Auction type"),
    MasterDescriptor.Of<AuctionStatus>("auction-statuses", "auction_status", "Auction status"),
    MasterDescriptor.Of<OutsourcingType>("outsourcing-types", "outsourcing_type", "Outsourcing type"),
    MasterDescriptor.Of<ContractStatus>("contract-statuses", "contract_status", "Contract status"),
    MasterDescriptor.Of<AgreementType>("agreement-types", "agreement_type", "Agreement type"),

    // ---- compliance ----
    MasterDescriptor.Of<EncroachmentStatus>("encroachment-statuses", "encroachment_status", "Encroachment status"),
    MasterDescriptor.Of<LitigationType>("litigation-types", "litigation_type", "Litigation type"),
    MasterDescriptor.Of<LitigationStatus>("litigation-statuses", "litigation_status", "Litigation status"),
    MasterDescriptor.Of<BuildingPlanType>("building-plan-types", "building_plan_type", "Building plan type"),
    MasterDescriptor.Of<BuildingPlanStatus>("building-plan-statuses", "building_plan_status", "Building plan status"),

    // ---- documents & custom fields ----
    MasterDescriptor.Of<DocumentType>("document-types", "document_type", "Document type", MasterExtraColumns.StorageFolder),
    MasterDescriptor.Of<AttributeGroup>("attribute-groups", "attribute_group", "Custom field group"),
  };

  private static readonly Dictionary<string, MasterDescriptor> BySlug =
      All.ToDictionary(d => d.Slug, StringComparer.OrdinalIgnoreCase);

  private static readonly Dictionary<Type, MasterDescriptor> ByType = All.ToDictionary(d => d.ClrType);

  public static MasterDescriptor Get(string slug) =>
      BySlug.TryGetValue(slug, out var descriptor)
          ? descriptor
          : throw new MasterDataNotFoundException(
              $"Unknown master data type '{slug}'. Known types: {string.Join(", ", All.Select(d => d.Slug))}.");

  public static MasterDescriptor For<T>() where T : MasterData => ByType[typeof(T)];
}

[Flags]
public enum MasterExtraColumns
{
  None = 0,
  FactorToBase = 1,
  IsBase = 2,
  StorageFolder = 4,
  RequiresRelationship = 8
}

/// How to reach one master table without knowing its CLR type at compile time. Every query is built
/// inside Of<T>, where T is the real entity type, so EF translates it like any typed query.
public sealed class MasterDescriptor
{
  public string Slug { get; }
  public string Table { get; }
  public string Label { get; }
  public Type ClrType { get; }
  public MasterExtraColumns Extras { get; }

  public required Func<IApplicationDbContext, bool, CancellationToken, Task<List<MasterData>>> ListAsync { get; init; }
  /// Tracked, for updates.
  public required Func<IApplicationDbContext, MasterId, CancellationToken, Task<MasterData?>> FindAsync { get; init; }
  public required Func<IApplicationDbContext, IReadOnlyCollection<MasterId>, CancellationToken, Task<List<MasterData>>> LoadAsync { get; init; }
  public required Func<IApplicationDbContext, MasterCode, CancellationToken, Task<bool>> CodeExistsAsync { get; init; }
  public required Func<IApplicationDbContext, MasterData, CancellationToken, Task> AddAsync { get; init; }
  public required Func<MasterId, MasterCode, Name, string?, int, MasterExtras, MasterData> Create { get; init; }

  private MasterDescriptor(string slug, string table, string label, Type clrType, MasterExtraColumns extras)
  {
    Slug = slug;
    Table = table;
    Label = label;
    ClrType = clrType;
    Extras = extras;
  }

  public static MasterDescriptor Of<T>(string slug, string table, string label, MasterExtraColumns extras = MasterExtraColumns.None)
      where T : MasterData, new() =>
      new(slug, table, label, typeof(T), extras)
      {
        ListAsync = async (context, includeInactive, cancellationToken) =>
          (await context.Set<T>().AsNoTracking()
            .Where(m => includeInactive || m.IsActive)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Code)
            .ToListAsync(cancellationToken)).Cast<MasterData>().ToList(),

        FindAsync = async (context, id, cancellationToken) =>
          await context.Set<T>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken),

        LoadAsync = async (context, ids, cancellationToken) =>
          (await context.Set<T>().AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken)).Cast<MasterData>().ToList(),

        CodeExistsAsync = (context, code, cancellationToken) =>
          context.Set<T>().AnyAsync(m => m.Code == code, cancellationToken),

        AddAsync = (context, master, cancellationToken) =>
          context.Set<T>().AddAsync((T)master, cancellationToken).AsTask(),

        Create = (id, code, name, description, sortOrder, masterExtras) =>
          MasterData.Create<T>(id, code, name, description, sortOrder, masterExtras)
      };

  public IReadOnlyList<string> ExtraFieldNames() =>
      Enum.GetValues<MasterExtraColumns>()
        .Where(flag => flag != MasterExtraColumns.None && Extras.HasFlag(flag))
        .Select(flag => char.ToLowerInvariant(flag.ToString()[0]) + flag.ToString()[1..])
        .ToList();
}
