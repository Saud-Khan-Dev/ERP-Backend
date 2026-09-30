/// The master codes the application itself relies on (schema guide, rule 9: compare on code, never id).
/// They are seeded and always kept present; admins may rename them but cannot deactivate them.
/// Everything else in the master tables is free for GDA to add, rename or retire.
public static class SystemMasterCodes
{
  /// Tenure given to a transferee who owned nothing before, when the transfer does not name one.
  public const string TenureOwned = "OWNED";

  /// Document type used for an owner's CNIC copy (property_owner.cnic_document_id).
  public const string DocumentCnicCopy = "CNIC_COPY";

  /// The base measurement unit; every *_base column is in this unit.
  public const string UnitSquareFeet = "SQFT";

  // ---- lifecycle statuses the workflows move records into ----
  public const string Active = "ACTIVE";
  public const string Draft = "DRAFT";
  public const string Cancelled = "CANCELLED";
  public const string Restored = "RESTORED";
  public const string Renewed = "RENEWED";
  public const string Terminated = "TERMINATED";
  public const string Expired = "EXPIRED";
  public const string Ended = "ENDED";
  public const string Planned = "PLANNED";
  public const string Awarded = "AWARDED";
  public const string Resolved = "RESOLVED";
  public const string Regularized = "REGULARIZED";
  public const string Pending = "PENDING";
  public const string Decided = "DECIDED";
  public const string Appealed = "APPEALED";
  public const string Submitted = "SUBMITTED";
  public const string Approved = "APPROVED";
  public const string Rejected = "REJECTED";
  public const string Revised = "REVISED";

  // ---- agreement types a violation can be linked to (s.28-A) ----
  public const string AgreementLease = "LEASE";
  public const string AgreementRent = "RENT";
  public const string AgreementSale = "SALE";

  // ---- litigation that only an authorized officer may file (Act s.30) ----
  public const string LitigationCriminalComplaint = "CRIMINAL_COMPLAINT";

  /// Extras: the extra columns the value needs when it has to be created (square feet is the base unit).
  public sealed record Required(Type MasterType, string Code, string Name, MasterExtras? Extras = null);

  /// Seeded into every database (also existing ones) and protected from deactivation.
  public static IReadOnlyList<Required> All { get; } = new Required[]
  {
    new(typeof(MeasurementUnit), UnitSquareFeet, "Square Feet", new MasterExtras(FactorToBase: 1m, IsBase: true)),
    new(typeof(TenureType), TenureOwned, "Owned"),
    new(typeof(DocumentType), DocumentCnicCopy, "CNIC Copy", new MasterExtras(StorageFolder: "cnic")),

    new(typeof(AllotmentStatus), Active, "Active"),
    new(typeof(AllotmentStatus), Cancelled, "Cancelled"),
    new(typeof(AllotmentStatus), Restored, "Restored"),

    new(typeof(LeaseStatus), Draft, "Draft"),
    new(typeof(LeaseStatus), Active, "Active"),
    new(typeof(LeaseStatus), Expired, "Expired"),
    new(typeof(LeaseStatus), Renewed, "Renewed"),
    new(typeof(LeaseStatus), Terminated, "Terminated"),
    new(typeof(LeaseStatus), Cancelled, "Cancelled"),

    new(typeof(RentalStatus), Active, "Active"),
    new(typeof(RentalStatus), Ended, "Ended"),
    new(typeof(RentalStatus), Cancelled, "Cancelled"),

    new(typeof(AuctionStatus), Planned, "Planned"),
    new(typeof(AuctionStatus), Awarded, "Awarded"),
    new(typeof(AuctionStatus), Cancelled, "Cancelled"),

    new(typeof(ContractStatus), Active, "Active"),
    new(typeof(ContractStatus), Expired, "Expired"),
    new(typeof(ContractStatus), Terminated, "Terminated"),

    new(typeof(EncroachmentStatus), Active, "Active"),
    new(typeof(EncroachmentStatus), Regularized, "Regularized"),
    new(typeof(EncroachmentStatus), Resolved, "Resolved / Removed"),

    new(typeof(LitigationStatus), Pending, "Pending"),
    new(typeof(LitigationStatus), Decided, "Decided"),
    new(typeof(LitigationStatus), Appealed, "Appealed"),

    new(typeof(BuildingPlanStatus), Submitted, "Submitted"),
    new(typeof(BuildingPlanStatus), Approved, "Approved"),
    new(typeof(BuildingPlanStatus), Rejected, "Rejected"),
    new(typeof(BuildingPlanStatus), Revised, "Revised"),

    new(typeof(AgreementType), AgreementLease, "Lease"),
    new(typeof(AgreementType), AgreementRent, "Rent"),
    new(typeof(AgreementType), AgreementSale, "Sale"),
  };

  public static bool IsRequired(Type masterType, string code) =>
      All.Any(r => r.MasterType == masterType && r.Code == code);
}
