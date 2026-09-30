/// Starting values for the master tables, taken from the schema's notes. Each table is only filled
/// when it is empty, so an administrator's edits are never overwritten.
///
/// Towns are left empty on purpose: GDA's six towns are not listed in the schema, so they are entered
/// from the settings screen.
public static class MasterDataSeed
{
  public sealed record Value(string Code, string Name, MasterExtras? Extras = null);

  private static Value[] List(params string[] names) =>
      names.Select(n => new Value(MasterCode.Of(n.Replace('/', ' ').Replace('.', ' ')).Value, n)).ToArray();

  public static IReadOnlyDictionary<string, Value[]> BySlug { get; } = new Dictionary<string, Value[]>
  {
    ["property-types"] = List("Residential", "Commercial", "Industrial", "Agricultural", "Institutional", "Open Land", "Mixed Use", "Other"),
    ["property-statuses"] = List("Open Plot", "Under Construction", "Construction Complete", "Vacant", "Occupied", "Encroached", "Under Litigation", "Other"),
    ["property-classifications"] = List("GDA Property", "Private Property", "Other"),
    ["measurement-units"] = new[]
    {
      new Value(SystemMasterCodes.UnitSquareFeet, "Square Feet", new MasterExtras(FactorToBase: 1m, IsBase: true)),
      new Value("SQM", "Square Meter", new MasterExtras(FactorToBase: 10.7639m)),
      new Value("MARLA", "Marla", new MasterExtras(FactorToBase: 272.25m)),
      new Value("KANAL", "Kanal", new MasterExtras(FactorToBase: 5445m)),
    },

    ["owner-types"] = List("Individual", "Company", "Government", "Trust", "Joint Entity", "Other"),
    ["tenure-types"] = List("Owned", "Leased", "Rented", "Other"),
    ["transfer-types"] = new[]
    {
      new Value("SALE", "Sale"),
      new Value("PURCHASE", "Purchase"),
      new Value("EXCHANGE", "Exchange"),
      new Value("INHERITANCE", "Inheritance", new MasterExtras(RequiresRelationship: true)),
      new Value("GIFT", "Gift", new MasterExtras(RequiresRelationship: true)),
      new Value("ADMINISTRATIVE_TRANSFER", "Administrative Transfer"),
      new Value("OTHER", "Other"),
    },
    ["contact-types"] = List("Mobile", "Landline", "Office", "WhatsApp", "Other"),
    ["encumbrance-types"] = List("Mortgage", "Lien", "Charge", "Other"),

    ["allotment-statuses"] = List("Active", "Cancelled", "Restored", "Surrendered", "Expired"),
    ["lease-statuses"] = List("Draft", "Active", "Expired", "Renewed", "Terminated", "Cancelled"),
    ["rental-statuses"] = List("Active", "Ended"),
    ["auction-types"] = List("Open Auction", "Sealed Bid", "Re-auction", "Other"),
    ["auction-statuses"] = List("Planned", "Announced", "Conducted", "Successful", "Unsuccessful", "Cancelled", "Awarded"),
    ["contract-statuses"] = List("Active", "Expired"),
    ["agreement-types"] = List("Lease", "Rent", "Sale", "Other"),

    ["encroachment-statuses"] = new[]
    {
      new Value("ACTIVE", "Active"),
      new Value("UNDER_NOTICE", "Under Notice"),
      new Value("REGULARIZED", "Regularized"),
      new Value("RESOLVED", "Resolved / Removed"),
      new Value("UNDER_LITIGATION", "Under Litigation"),
    },
    ["litigation-types"] = new[]
    {
      new Value("CIVIL_SUIT", "Civil Suit"),
      new Value("WRIT", "Writ"),
      new Value("CRIMINAL_COMPLAINT", "Criminal Complaint (Act s.30)"),
      new Value("DISPUTE_RESOLUTION", "Dispute Resolution Committee (Act s.9)"),
      new Value("OTHER", "Other"),
    },
    ["litigation-statuses"] = List("Pending", "Decided", "Withdrawn", "Settled", "Appealed", "Closed", "Other"),
    ["building-plan-types"] = List("Site Plan", "Building Plan", "Revised Building Plan", "Completion Plan", "Other"),
    ["building-plan-statuses"] = List("Submitted", "Under Review", "Approved", "Rejected", "Revised", "Withdrawn"),

    ["document-types"] = new[]
    {
      new Value("NOTESHEET", "Notesheet", new MasterExtras(StorageFolder: "notesheets")),
      new Value("OWNERSHIP_DOCUMENT", "Ownership Document", new MasterExtras(StorageFolder: "ownership")),
      new Value("SITE_BUILDING_PLAN", "Site/Building Plan", new MasterExtras(StorageFolder: "plans")),
      new Value("NOTICE_LETTER", "Notice/Letter", new MasterExtras(StorageFolder: "notices")),
      new Value(SystemMasterCodes.DocumentCnicCopy, "CNIC Copy", new MasterExtras(StorageFolder: "cnic")),
      new Value("AGREEMENT", "Agreement", new MasterExtras(StorageFolder: "agreements")),
      new Value("COURT_ORDER", "Court Order", new MasterExtras(StorageFolder: "litigation")),
      new Value("OTHER", "Other", new MasterExtras(StorageFolder: "other")),
    },
    ["attribute-groups"] = List("General"),
  };
}
