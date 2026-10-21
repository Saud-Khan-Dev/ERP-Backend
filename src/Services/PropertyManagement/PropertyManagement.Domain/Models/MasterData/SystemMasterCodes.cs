/// The master codes the application itself relies on (schema guide, rule 9: compare on code, never id).
/// They are seeded and always kept present; admins may rename them but cannot deactivate them.
/// Everything else in the master tables is free for GDA to add, rename or retire.
public static class SystemMasterCodes
{
  /// Tenure given to a transferee who owned nothing before, when the transfer does not name one.
  public const string TenureOwned = "OWNED";

  /// Document type used for an owner's CNIC copy (property_owner.cnic_document_id). Applies to OWNER.
  public const string DocumentCnicCopy = "CNIC_COPY";

  // ---- owner types: what an owner must or must not carry (PropertyOwner) ----
  /// A person: needs a CNIC.
  public const string OwnerTypeIndividual = "INDIVIDUAL";
  /// Organizations: no CNIC and no father / husband name (they are identified by their NTN).
  public const string OwnerTypeCompany = "COMPANY";
  public const string OwnerTypeTrust = "TRUST";
  public const string OwnerTypeGovernment = "GOVERNMENT";
  public const string OwnerTypeJointEntity = "JOINT_ENTITY";

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
  public const string Withdrawn = "WITHDRAWN";
  public const string Settled = "SETTLED";
  public const string Closed = "CLOSED";
  public const string Successful = "SUCCESSFUL";
  public const string Unsuccessful = "UNSUCCESSFUL";
  public const string UnderNotice = "UNDER_NOTICE";
  public const string UnderLitigation = "UNDER_LITIGATION";
  public const string UnderReview = "UNDER_REVIEW";
  public const string Announced = "ANNOUNCED";
  public const string Conducted = "CONDUCTED";

  /// The only classification that can be allotted (PropertyAllotment).
  public const string ClassificationGdaProperty = "GDA_PROPERTY";
  public const string Surrendered = "SURRENDERED";

  /// The auction type a re-auction of an unsuccessful or cancelled auction gets.
  public const string AuctionReAuction = "RE_AUCTION";

  // ---- agreement types a violation can be linked to (s.28-A) ----
  public const string AgreementLease = "LEASE";
  public const string AgreementRent = "RENT";
  public const string AgreementSale = "SALE";
  public const string AgreementAllotment = "ALLOTMENT";

  // ---- codes other modules rely on (Taxation maps its categories and fees onto them) ----
  public const string PropertyTypeResidential = "RESIDENTIAL";
  public const string PropertyTypeCommercial = "COMMERCIAL";
  public const string BuildingPlanCompletion = "COMPLETION_PLAN";

  // ---- litigation that only an authorized officer may file (Act s.30) ----
  public const string LitigationCriminalComplaint = "CRIMINAL_COMPLAINT";

  /// Extras: the extra columns the value needs when it has to be created (square feet is the base unit).
  public sealed record Required(Type MasterType, string Code, string Name, MasterExtras? Extras = null);

  /// Seeded into every database (also existing ones) and protected from deactivation.
  public static IReadOnlyList<Required> All { get; } = new Required[]
  {
    new(typeof(MeasurementUnit), UnitSquareFeet, "Square Feet", new MasterExtras(FactorToBase: 1m, IsBase: true)),
    new(typeof(TenureType), TenureOwned, "Owned"),
    new(typeof(DocumentType), DocumentCnicCopy, "CNIC Copy", new MasterExtras(StorageFolder: "cnic", AppliesTo: DocumentScope.Owner)),

    new(typeof(OwnerType), OwnerTypeIndividual, "Individual"),
    new(typeof(OwnerType), OwnerTypeCompany, "Company"),
    new(typeof(OwnerType), OwnerTypeGovernment, "Government"),
    new(typeof(OwnerType), OwnerTypeTrust, "Trust"),
    new(typeof(OwnerType), OwnerTypeJointEntity, "Joint Entity"),

    new(typeof(AllotmentStatus), Active, "Active"),
    new(typeof(AllotmentStatus), Cancelled, "Cancelled"),
    new(typeof(AllotmentStatus), Restored, "Restored"),
    new(typeof(AllotmentStatus), Surrendered, "Surrendered"),
    new(typeof(AllotmentStatus), Expired, "Expired"),
    new(typeof(PropertyClassification), ClassificationGdaProperty, "GDA Property"),

    new(typeof(LeaseStatus), Draft, "Draft"),
    new(typeof(LeaseStatus), Active, "Active"),
    new(typeof(LeaseStatus), Expired, "Expired"),
    new(typeof(LeaseStatus), Renewed, "Renewed"),
    new(typeof(LeaseStatus), Terminated, "Terminated"),
    new(typeof(LeaseStatus), Cancelled, "Cancelled"),
    new(typeof(LeaseStatus), Withdrawn, "Withdrawn"),

    new(typeof(RentalStatus), Draft, "Draft"),
    new(typeof(RentalStatus), Active, "Active"),
    new(typeof(RentalStatus), Ended, "Ended"),
    new(typeof(RentalStatus), Expired, "Expired"),
    new(typeof(RentalStatus), Terminated, "Terminated"),
    new(typeof(RentalStatus), Cancelled, "Cancelled"),
    new(typeof(RentalStatus), Withdrawn, "Withdrawn"),

    new(typeof(AuctionStatus), Planned, "Planned"),
    new(typeof(AuctionStatus), Announced, "Announced"),
    new(typeof(AuctionStatus), Conducted, "Conducted"),
    new(typeof(AuctionStatus), Awarded, "Awarded"),
    new(typeof(AuctionStatus), Cancelled, "Cancelled"),
    new(typeof(AuctionStatus), Unsuccessful, "Unsuccessful"),
    new(typeof(AuctionType), AuctionReAuction, "Re-auction"),

    new(typeof(ContractStatus), Draft, "Draft"),
    new(typeof(ContractStatus), Active, "Active"),
    new(typeof(ContractStatus), Expired, "Expired"),
    new(typeof(ContractStatus), Renewed, "Renewed"),
    new(typeof(ContractStatus), Terminated, "Terminated"),
    new(typeof(ContractStatus), Withdrawn, "Withdrawn"),

    new(typeof(EncroachmentStatus), Active, "Active"),
    new(typeof(EncroachmentStatus), Regularized, "Regularized"),
    new(typeof(EncroachmentStatus), Resolved, "Resolved / Removed"),
    new(typeof(EncroachmentStatus), UnderNotice, "Under Notice"),
    new(typeof(EncroachmentStatus), UnderLitigation, "Under Litigation"),

    new(typeof(LitigationStatus), Pending, "Pending"),
    new(typeof(LitigationStatus), Decided, "Decided"),
    new(typeof(LitigationStatus), Appealed, "Appealed"),
    new(typeof(LitigationStatus), Withdrawn, "Withdrawn"),
    new(typeof(LitigationStatus), Settled, "Settled"),
    new(typeof(LitigationStatus), Closed, "Closed"),

    new(typeof(BuildingPlanStatus), Submitted, "Submitted"),
    new(typeof(BuildingPlanStatus), UnderReview, "Under Review"),
    new(typeof(BuildingPlanStatus), Approved, "Approved"),
    new(typeof(BuildingPlanStatus), Rejected, "Rejected"),
    new(typeof(BuildingPlanStatus), Revised, "Revised"),
    new(typeof(BuildingPlanStatus), Withdrawn, "Withdrawn"),

    new(typeof(AgreementType), AgreementLease, "Lease"),
    new(typeof(AgreementType), AgreementRent, "Rent"),
    new(typeof(AgreementType), AgreementSale, "Sale"),
    new(typeof(AgreementType), AgreementAllotment, "Allotment"),

    new(typeof(PropertyType), PropertyTypeResidential, "Residential"),
    new(typeof(PropertyType), PropertyTypeCommercial, "Commercial"),
    new(typeof(BuildingPlanType), BuildingPlanCompletion, "Completion Plan"),
  };

  public static bool IsRequired(Type masterType, string code) =>
      All.Any(r => r.MasterType == masterType && r.Code == code);
}
