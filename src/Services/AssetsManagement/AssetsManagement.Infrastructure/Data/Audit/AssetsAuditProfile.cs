/// How Assets' records read in its audit trail. Every child of an asset hangs Under the asset, so the asset's History
/// lists it; setup records (statuses, methods, the structure, the fields) are named by their names - the codes are a
/// configuration detail nobody reads.
public static class AssetsAuditProfile
{
  public static void Configure(AuditProfile profile)
  {
    // rows written in bulk or mirrored from another row - their acts write their own lines
    profile.Skip<AssetAttributeValue>();   // the typed copy of the field values, rewritten on every save
    profile.Skip<AssetAttributeHistory>(); // the old field-change archive (no longer written)
    profile.Skip<AssetLifecycleEvent>();   // the timeline: each act's own lines say it; hand-recorded events get "Recorded"
    profile.Skip<AssetDepreciationEntry>(); // months: generated in bulk; posting and reversing get their own lines

    // ---- the asset and what hangs off it ----
    profile.Entity<Asset>(e => e.Kind(AssetAuditKinds.Asset).Code(a => a.AssetCode.Value).Label(a => a.Name.Value)
      // field values are written one line per field by the act (names and choices in words); deleting is a Removed line
      .Ignore("_extraAttributes", "ExtraAttributes", "AttributesValidatedAt", "IsActive", "DeletedAt", "DeletedBy", "DeletionReason",
        "DepartmentId", "CustodianId")
      .Field("AssetCode", "Asset tag").Field("AssetClassId", "Class").Field("AssetTypeId", "Type").Field("CategoryId", "Category")
      .Field("StatusId", "Status").Field("DepartmentName", "Department").Field("CustodianName", "Custodian")
      .Field("CustodianNumber", "Custodian's employee number").Field("CurrentLocationId", "Location"));

    profile.Entity<AssetAssignment>(e => e.Kind(AssetAuditKinds.Move).Under<Asset>(x => x.AssetId)
      .Label(x => x.ExpectedReturnDate.HasValue ? "Loan" : "Transfer")
      .Ignore("AssetId", "FromDepartmentId", "ToDepartmentId", "FromCustodianId", "ToCustodianId", "RecordedBy", "RecordedByName")
      .Field("FromDepartmentName", "From department").Field("ToDepartmentName", "To department")
      .Field("FromCustodianName", "From custodian").Field("FromCustodianNumber", "From custodian's employee number")
      .Field("ToCustodianName", "To custodian").Field("ToCustodianNumber", "To custodian's employee number")
      .Field("FromLocationId", "From location").Field("ToLocationId", "To location").Field("AssignmentDate", "Moved on")
      .Field("ExpectedReturnDate", "Due back on").Field("ActualReturnDate", "Returned on"));

    profile.Entity<AssetAcquisition>(e => e.Kind(AssetAuditKinds.Purchase).Under<Asset>(x => x.AssetId).Ignore("AssetId")
      .Field("AcquisitionDate", "Acquired on").Field("AcquisitionCost", "Cost").Field("CurrencyCode", "Currency")
      .Field("BaseCurrencyCost", "Cost in base currency").Field("SupplierName", "Supplier").Field("PurchaseReference", "Purchase reference")
      .Field("AcquisitionType", "How acquired").Field("WarrantyStartDate", "Warranty from").Field("WarrantyExpiryDate", "Warranty until"));

    profile.Entity<AssetAttachment>(e => e.Kind(AssetAuditKinds.Document).Under<Asset>(x => x.AssetId)
      .Label(x => string.IsNullOrWhiteSpace(x.Title) ? x.OriginalFileName : x.Title)
      .Ignore("AssetId", "StoredFileName", "StoragePath", "ChecksumSha256", "MimeType")
      .Field("AttachmentType", "Kind").Field("OriginalFileName", "File").Field("FileSize", "Size (bytes)")
      .Field("IsPrimaryImage", "Main photo").Field("Title", "What it is"));

    profile.Entity<AssetDepreciationSchedule>(e => e.Kind(AssetAuditKinds.DepreciationSchedule).Under<Asset>(x => x.AssetId).Ignore("AssetId")
      .Field("MethodId", "Method").Field("UsefulLifeMonths", "Useful life (months)").Field("SalvageValue", "Value at the end")
      .Field("OpeningBookValue", "Starting book value").Field("DepreciableBase", "To write off").Field("DecliningRate", "Yearly rate")
      .Field("StartDate", "Starts on").Field("EndDate", "Stops before").Field("IsActive", "Running"));

    profile.Entity<AssetValuation>(e => e.Kind(AssetAuditKinds.Valuation).Under<Asset>(x => x.AssetId)
      .Ignore("AssetId", "RecordedBy", "RecordedByName")
      .Field("ValuationDate", "Valued on").Field("CurrencyCode", "Currency").Field("ValuationMethod", "Method").Field("ValuedByName", "Valued by"));

    profile.Entity<AssetDisposal>(e => e.Kind(AssetAuditKinds.Disposal).Under<Asset>(x => x.AssetId)
      // who proposed, approved, rejected or cancelled is the line's own actor (and a verb line)
      .Ignore("AssetId", "ProposedBy", "ProposedByName", "ProposedAt", "ApprovedBy", "ApprovedByName", "ApprovedAt", "DecidedBy",
        "DecidedByName", "DecidedAt", "CancelledBy", "CancelledByName", "CancelledAt")
      .Field("State", "Stage").Field("DisposalDate", "Disposed on").Field("DisposalMethodId", "How").Field("ToStatusId", "Final status")
      .Field("DisposalValue", "Proceeds").Field("CurrencyCode", "Currency").Field("NetBookValueAtDisposal", "Book value on the day")
      .Field("GainLoss", "Gain / loss").Field("BuyerInfo", "Buyer").Field("DecisionNote", "Note").Field("CancellationReason", "Why cancelled"));

    // ---- lists (named by their names) ----
    profile.Entity<AssetStatus>(e => e.Kind("Status").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("IsTerminal", "Final status (closes the record)").Field("AllowsAssignment", "Can be moved or issued").Field("Color", "Colour")
      .Field("DisplayOrder", "Order").Field("IsActive", "In use"));
    profile.Entity<CurrencyLookup>(e => e.Kind("Currency").Code(x => x.Id.Value).Label(x => x.Id.Value)
      .Field("MinorUnits", "Decimal places").Field("IsActive", "In use"));
    profile.Entity<Location>(e => e.Kind("Location").Code(x => x.Name.Value).Label(x => x.Name.Value).Ignore("Path")
      .Field("ParentLocationId", "Inside").Field("LocationType", "Kind of place").Field("IsActive", "In use"));
    profile.Entity<LifecycleEventType>(e => e.Kind("Event type").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("Purpose", "Written by the act").Field("IsSystem", "Written by the system").Field("IsActive", "In use"));
    profile.Entity<DepreciationMethod>(e => e.Kind("Depreciation method").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("Kind", "Worked out").Field("IsSystem", "Built in").Field("IsActive", "In use"));
    profile.Entity<DisposalMethod>(e => e.Kind("Disposal method").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("RequiresValue", "Proceeds required").Field("AllowsValue", "Proceeds allowed").Field("IsActive", "In use"));

    // ---- the structure ----
    profile.Entity<AssetClass>(e => e.Kind("Asset class").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("DisplayOrder", "Order").Field("IsActive", "In use"));
    profile.Entity<AssetType>(e => e.Kind("Asset type").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("AssetClassId", "Class").Field("IsDepreciable", "Depreciates").Field("RequiresLocation", "Location required")
      .Field("RequiresCustodian", "Custodian required").Field("DisplayOrder", "Order").Field("IsActive", "In use"));
    profile.Entity<AssetCategory>(e => e.Kind("Category").Code(x => x.Name.Value).Label(x => x.Name.Value).Ignore("Path", "Depth", "IsLeaf")
      .Field("AssetClassId", "Class").Field("AssetTypeId", "Type").Field("ParentCategoryId", "Inside").Field("DisplayOrder", "Order")
      .Field("IsActive", "In use"));

    // ---- the fields ----
    profile.Entity<AttributeDefinition>(e => e.Kind("Field").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("DataType", "Kind of value").Field("OptionSetId", "Choices from").Field("ReferenceEntity", "Points to")
      .Field("NumericPrecision", "Digits").Field("NumericScale", "Decimal places").Field("MinNumber", "Smallest").Field("MaxNumber", "Largest")
      .Field("MinLength", "Shortest").Field("MaxLength", "Longest").Field("MinDate", "Earliest").Field("MaxDate", "Latest")
      .Field("RegexPattern", "Pattern").Field("IsUniquePerCategory", "Unique in its category").Field("ValidationMessage", "Message")
      .Field("IsMultiValue", "Several values").Field("IsPii", "Personal data").Field("IsSystem", "Built in").Field("IsActive", "In use"));
    profile.Entity<OptionSet>(e => e.Kind("Choice list").Code(x => x.Label.Value).Label(x => x.Label.Value)
      .Field("IsSystem", "Built in").Field("IsActive", "In use"));
    profile.Entity<OptionSetValue>(e => e.Kind("Choice").Code(x => x.Label).Label(x => x.Label).Under<OptionSet>(x => x.OptionSetId)
      .Ignore("OptionSetId").Field("Value", "Stored value").Field("ParentValueId", "Under").Field("Color", "Colour")
      .Field("DisplayOrder", "Order").Field("IsActive", "Offered"));
    profile.Entity<AttributeGroup>(e => e.Kind("Form section").Code(x => x.Name.Value).Label(x => x.Name.Value)
      .Field("DisplayOrder", "Order").Field("IsCollapsible", "Can be folded").Field("IsActive", "In use"));
    profile.Entity<AttributeAssignment>(e => e.Kind("Field placement").Under<AttributeDefinition>(x => x.AttributeDefinitionId)
      .Field("AttributeDefinitionId", "Field").Field("Scope", "Level").Field("AssetClassId", "Class").Field("AssetTypeId", "Type")
      .Field("CategoryId", "Category").Field("AssetId", "Asset").Field("AttributeGroupId", "Section").Field("LabelOverride", "Label here")
      .Field("IsRequired", "Required").Field("IsReadonly", "Read-only").Field("IsSearchable", "Searchable").Field("IsFilterable", "Filter")
      .Field("IsVisibleInList", "In the list").Field("InheritToChildren", "Below this too").Field("DefaultValue", "Default")
      .Field("DisplayOrder", "Order").Field("DependsOnAssignmentId", "Shown when (field)").Field("DependsOnValue", "Shown when (value)")
      .Field("IsActive", "In use").Field("IsHidden", "Not used here"));
  }
}
