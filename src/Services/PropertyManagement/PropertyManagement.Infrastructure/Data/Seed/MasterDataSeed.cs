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

  private static MasterExtras Owner(string folder) => new(StorageFolder: folder, AppliesTo: DocumentScope.Owner);

  /// The kinds of paper an owner files about themselves (applies_to OWNER, stored under /owner-documents/OWN-xxxxx/).
  /// CNIC_COPY is a system code (cnic_document_id points at it); the rest are ordinary values GDA may rename or
  /// retire. Declared before BySlug, which includes them (static initializers run in order).
  public static Value[] OwnerDocumentTypes { get; } =
  [
    new(SystemMasterCodes.DocumentCnicCopy, "CNIC Copy", Owner("cnic")),
    new("NICOP_COPY", "NICOP Copy", Owner("nicop")),
    new("PASSPORT_COPY", "Passport Copy", Owner("passport")),
    new("NTN_CERTIFICATE", "NTN Certificate", Owner("ntn")),
    new("SECP_INCORPORATION", "SECP Certificate of Incorporation", Owner("incorporation")),
    new("MEMORANDUM_ARTICLES", "Memorandum & Articles of Association", Owner("memorandum-articles")),
    new("BOARD_RESOLUTION", "Board Resolution / Authority to Sign", Owner("board-resolutions")),
    new("PARTNERSHIP_DEED", "Partnership Deed", Owner("partnership-deeds")),
    new("TRUST_DEED", "Trust Deed", Owner("trust-deeds")),
    new("SUCCESSION_CERTIFICATE", "Succession Certificate", Owner("succession")),
    new("FAMILY_REGISTRATION_CERT", "NADRA Family Registration Certificate", Owner("frc")),
    new("DEATH_CERTIFICATE", "Death Certificate", Owner("death-certificates")),
    new("POWER_OF_ATTORNEY", "Power of Attorney", Owner("power-of-attorney")),
    new("GUARDIANSHIP_CERTIFICATE", "Guardianship Certificate", Owner("guardianship")),
    new("DOMICILE_CERTIFICATE", "Domicile Certificate", Owner("domicile")),
    new("PHOTOGRAPH", "Photograph", Owner("photographs")),
    new("GOVERNMENT_AUTHORITY_LETTER", "Government Authority Letter", Owner("authority-letters")),
  ];

  public static IReadOnlyDictionary<string, Value[]> BySlug { get; } = new Dictionary<string, Value[]>
  {
    ["property-types"] = List("Residential", "Commercial", "Industrial", "Agricultural", "Institutional", "Open Land", "Mixed Use"),
    ["property-statuses"] = List("Open Plot", "Under Construction", "Construction Complete", "Vacant", "Occupied", "Encroached", "Under Litigation"),
    ["property-classifications"] = List("GDA Property", "Private Property"),
    ["measurement-units"] = new[]
    {
      new Value(SystemMasterCodes.UnitSquareFeet, "Square Feet", new MasterExtras(FactorToBase: 1m, IsBase: true)),
      new Value("SQM", "Square Meter", new MasterExtras(FactorToBase: 10.7639m)),
      new Value("MARLA", "Marla", new MasterExtras(FactorToBase: 272.25m)),
      new Value("KANAL", "Kanal", new MasterExtras(FactorToBase: 5445m)),
    },

    ["owner-types"] = List("Individual", "Company", "Government", "Trust", "Joint Entity"),
    ["tenure-types"] = List("Owned", "Leased", "Rented"),
    ["transfer-types"] = new[]
    {
      new Value("SALE", "Sale"),
      new Value("PURCHASE", "Purchase"),
      new Value("EXCHANGE", "Exchange"),
      new Value("INHERITANCE", "Inheritance", new MasterExtras(RequiresRelationship: true)),
      new Value("GIFT", "Gift", new MasterExtras(RequiresRelationship: true)),
      new Value("ADMINISTRATIVE_TRANSFER", "Administrative Transfer"),
    },
    ["contact-types"] = List("Mobile", "Landline", "Office", "WhatsApp"),
    ["encumbrance-types"] = List("Mortgage", "Lien", "Charge"),

    ["allotment-statuses"] = List("Active", "Cancelled", "Restored", "Surrendered", "Expired"),
    ["lease-statuses"] = List("Draft", "Active", "Expired", "Renewed", "Terminated", "Cancelled"),
    ["rental-statuses"] = List("Draft", "Active", "Ended", "Expired", "Terminated", "Cancelled"),
    ["auction-types"] = List("Open Auction", "Sealed Bid", "Re-auction"),
    // no "Successful": an auction succeeds only by being awarded (AWARDED records the winner)
    ["auction-statuses"] = List("Planned", "Announced", "Conducted", "Unsuccessful", "Cancelled", "Awarded"),
    ["contract-statuses"] = List("Draft", "Active", "Expired", "Renewed", "Terminated"),
    ["agreement-types"] = List("Lease", "Rent", "Sale", "Allotment"),

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
    },
    ["litigation-statuses"] = List("Pending", "Decided", "Withdrawn", "Settled", "Appealed", "Closed"),
    ["building-plan-types"] = List("Site Plan", "Building Plan", "Revised Building Plan", "Completion Plan"),
    ["building-plan-statuses"] = List("Submitted", "Under Review", "Approved", "Rejected", "Revised", "Withdrawn"),

    ["document-types"] =
    [
      // a property's papers (applies_to PROPERTY)
      new Value("NOTESHEET", "Notesheet", new MasterExtras(StorageFolder: "notesheets")),
      new Value("OWNERSHIP_DOCUMENT", "Ownership Document", new MasterExtras(StorageFolder: "ownership")),
      new Value("SITE_BUILDING_PLAN", "Site/Building Plan", new MasterExtras(StorageFolder: "plans")),
      new Value("NOTICE_LETTER", "Notice/Letter", new MasterExtras(StorageFolder: "notices")),
      new Value("AGREEMENT", "Agreement", new MasterExtras(StorageFolder: "agreements")),
      new Value("COURT_ORDER", "Court Order", new MasterExtras(StorageFolder: "litigation")),
      // an owner's own papers (applies_to OWNER)
      .. OwnerDocumentTypes,
    ],
    ["attribute-groups"] = List("General"),
  };

  /// Values added after the first release: unlike the lists above (written only into an empty table), each is
  /// added by code to databases seeded before it existed. One already there - even renamed or deactivated by
  /// an administrator - is left as it is.
  public static IReadOnlyDictionary<string, Value[]> AddedLater { get; } = new Dictionary<string, Value[]>
  {
    ["document-types"] = OwnerDocumentTypes,
  };
}
