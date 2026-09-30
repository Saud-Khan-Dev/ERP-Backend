// The master tables with no extra columns. Each is its own table (see MasterDataConfiguration);
// the class only exists so foreign keys and queries are typed.

// ---- property ----
public sealed class Town : MasterData { }
public sealed class PropertyType : MasterData { }
public sealed class PropertyStatus : MasterData { }
public sealed class PropertyClassification : MasterData { }

// ---- owners & ownership ----
public sealed class OwnerType : MasterData { }
public sealed class TenureType : MasterData { }
public sealed class ContactType : MasterData { }
public sealed class EncumbranceType : MasterData { }

// ---- management ----
public sealed class AllotmentType : MasterData { }
public sealed class AllotmentStatus : MasterData { }
public sealed class LeaseType : MasterData { }
public sealed class LeaseStatus : MasterData { }
public sealed class RentalType : MasterData { }
public sealed class RentalStatus : MasterData { }
public sealed class AuctionType : MasterData { }
public sealed class AuctionStatus : MasterData { }
public sealed class OutsourcingType : MasterData { }
public sealed class ContractStatus : MasterData { }
public sealed class AgreementType : MasterData { }

// ---- compliance ----
public sealed class EncroachmentStatus : MasterData { }
public sealed class LitigationType : MasterData { }
public sealed class LitigationStatus : MasterData { }
public sealed class BuildingPlanType : MasterData { }
public sealed class BuildingPlanStatus : MasterData { }

// ---- custom fields ----
/// Groups the admin-defined custom fields on the property form (General, Utilities ...).
public sealed class AttributeGroup : MasterData { }
