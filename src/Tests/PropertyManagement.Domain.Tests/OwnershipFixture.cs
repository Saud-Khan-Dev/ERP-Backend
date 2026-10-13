using static Fixture;

/// Builders for the ownership, transfer and allotment tests: a property whose classification is at hand,
/// ownership rows, the ownership record the rules read, and transfer parties. "Today" is fixed.
internal static class OwnershipFixture
{
  public static readonly DateOnly Today = D("2026-10-13");
  public static readonly TenureType Owned = Master<TenureType>("OWNED");
  public static readonly TransferType Sale = Master<TransferType>("SALE");

  public static readonly AllotmentType Plot = Master<AllotmentType>("PLOT");
  public static readonly AllotmentStatus Active = Master<AllotmentStatus>(SystemMasterCodes.Active);
  public static readonly AllotmentStatus Cancelled = Master<AllotmentStatus>(SystemMasterCodes.Cancelled);
  public static readonly AllotmentStatus Restored = Master<AllotmentStatus>(SystemMasterCodes.Restored);
  public static readonly AllotmentStatus Surrendered = Master<AllotmentStatus>(PropertyAllotment.SurrenderedCode);
  public static readonly AllotmentStatus Expired = Master<AllotmentStatus>(SystemMasterCodes.Expired);

  /// A property with its classification (GDA_PROPERTY unless told otherwise).
  public static (Property Property, PropertyClassification Classification) NewPlot(string classificationCode = PropertyAllotment.GdaPropertyClassificationCode)
  {
    var classification = Master<PropertyClassification>(classificationCode);
    var property = global::Property.Create(
      PropertyId.New(), BusinessCode.Of("PROP-00001"), Name.Of("Plot 7, Nathia Gali"),
      Master<Town>("NATHIA_GALI"), Master<PropertyType>("RESIDENTIAL"), Master<PropertyStatus>("OPEN_PLOT"),
      classification, null, null, null, null);
    return (property, classification);
  }

  public static PropertyOwnership Own(Property property, PropertyOwner owner, decimal share, string from = "2020-01-01") =>
      PropertyOwnership.Register(OwnershipId.New(), property, owner, Owned, share, D(from), null, null, null);

  public static OwnershipRecord Record(
      Property property,
      IEnumerable<PropertyOwnership> rows,
      IEnumerable<PropertyOwner> owners,
      IEnumerable<PropertyTransfer>? pending = null,
      IEnumerable<PropertyEncumbrance>? charges = null,
      DateOnly? today = null) =>
      new(property, rows, owners, pending ?? Array.Empty<PropertyTransfer>(), charges ?? Array.Empty<PropertyEncumbrance>(), today ?? Today);

  public static PropertyTransfer.PartyInput Gives(PropertyOwner owner, decimal share) => new(owner.Id, TransferPartyRole.Transferor, share);

  public static PropertyTransfer.PartyInput Gets(PropertyOwner owner, decimal share) => new(owner.Id, TransferPartyRole.Transferee, share);

  public static PropertyTransfer Initiate(OwnershipRecord record, string date, string no, params PropertyTransfer.PartyInput[] parties) =>
      PropertyTransfer.Initiate(TransferId.New(), BusinessCode.Of(no), Sale, D(date), "MUT-1", null, null, null, parties, record);

  public static PropertyTransfer.OwnershipChange Approve(PropertyTransfer transfer, OwnershipRecord record, string? approvalDate = null) =>
      transfer.Approve("Director Estate", approvalDate is null ? record.Today : D(approvalDate), "ORDER-9", "clerk1", record, Owned);

  public static PropertyEncumbrance Charge(Property property, PropertyOwnership? share, string holder = "Bank of Khyber", string? reference = "MRT-1") =>
      PropertyEncumbrance.Register(EncumbranceId.New(), property, Master<EncumbranceType>("MORTGAGE"), share, holder, null, reference,
        1_000_000m, D("2024-01-01"), null, null);

  public static PropertyAllotment Allot(Property property, PropertyClassification classification, PropertyOwner allottee,
      string date = "2025-01-01", string no = "ALT-00001", DateOnly? expiry = null, PropertyAllotment.InForce? inForce = null) =>
      PropertyAllotment.Allot(AllotmentId.New(), property, classification, allottee, BusinessCode.Of(no), Plot, Active,
        D(date), null, expiry, "LTR-1", null, null, inForce, Today);

  /// Puts a transfer in the APPROVED state the way rows approved before "approve = complete" are stored.
  public static void MakeLegacyApproved(PropertyTransfer transfer) =>
      typeof(PropertyTransfer).GetProperty(nameof(PropertyTransfer.TransferStatus))!.SetValue(transfer, TransferStatus.Approved);

  /// The sentence the clerk reads: DomainException wraps it as `Domain Exception "<sentence> " throws from Domain layer`
  /// and the frontend unwraps it the same way.
  public static string Message(Action act)
  {
    var message = Assert.Throws<DomainException>(act).Message;
    var match = System.Text.RegularExpressions.Regex.Match(message, "^Domain Exception \"(.*) \" throws from Domain layer$",
      System.Text.RegularExpressions.RegexOptions.Singleline);
    return match.Success ? match.Groups[1].Value : message;
  }
}
