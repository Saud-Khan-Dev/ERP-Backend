/// How the Property module's records read in the audit trail (audit_entry): the kind of each record in words, its code
/// and name, and the record it hangs off - every record on a property hangs off the PROPERTY (so the property's History
/// lists it, its case parties, hearings, bids and transfer parties too), an owner's contacts, addresses and own papers off
/// the OWNER. Registered by the Infrastructure (AddAuditTrail<ApplicationDbContext>("Property", PropertyAudit.Configure)).
public static class PropertyAudit
{
  public const string Module = "Property";

  // the kinds the History tab filters by (legal matters need PROPERTY_LEGAL.VIEW)
  public const string Encroachment = "Encroachment";
  public const string CourtCase = "Court case";
  public const string CaseParty = "Case party";
  public const string Hearing = "Hearing";
  public const string Appeal = "Appeal";

  public static void Configure(AuditProfile profile)
  {
    profile
      .Entity<Property>(e => e.Kind("Property").Code(x => x.PropertyCode.Value).Label(x => x.PropertyName.Value))
      .Entity<PropertyStatusHistory>(e => e.Kind("Status").Under<Property>(x => x.PropertyId).Label(x => $"from {x.EffectiveFrom:yyyy-MM-dd}"))
      .Entity<PropertyMeasurement>(e => e.Kind("Measurement").Under<Property>(x => x.PropertyId)
        .Label(x => x.MeasuredOn is { } on ? $"measured {on:yyyy-MM-dd}" : null))
      .Entity<PropertyAreaRegularization>(e => e.Kind("Area regularization").Under<Property>(x => x.PropertyId))

      .Entity<PropertyOwner>(e => e.Kind("Owner").Code(x => x.OwnerCode.Value).Label(x => x.OwnerName.Value))
      .Entity<OwnerContact>(e => e.Kind("Owner contact").Under<PropertyOwner>(x => x.OwnerId).Label(x => x.ContactNumber))
      .Entity<OwnerAddress>(e => e.Kind("Owner address").Under<PropertyOwner>(x => x.OwnerId).Label(x => x.FullAddress))

      .Entity<PropertyOwnership>(e => e.Kind("Ownership").Under<Property>(x => x.PropertyId))
      .Entity<PropertyTransfer>(e => e.Kind("Transfer").Code(x => x.TransferNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<PropertyTransferParty>(e => e.Kind("Transfer party").Code(x => x.Transfer == null ? null : x.Transfer.TransferNo.Value)
        .Under<Property>(x => x.Transfer == null ? null : x.Transfer.PropertyId))
      .Entity<PropertyEncumbrance>(e => e.Kind("Encumbrance").Under<Property>(x => x.PropertyId))

      .Entity<PropertyAllotment>(e => e.Kind("Allotment").Code(x => x.AllotmentNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<PropertyLease>(e => e.Kind("Lease").Code(x => x.LeaseNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<PropertyRental>(e => e.Kind("Rental").Code(x => x.RentalNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<PropertyOutsourcing>(e => e.Kind("Outsourcing contract").Code(x => x.ContractNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<AgreementViolation>(e => e.Kind("Violation").Code(x => x.NoticeNo).Under<Property>(x => x.PropertyId)
        .Label(x => $"violation #{x.OccurrenceNo} of {x.ViolationDate:yyyy-MM-dd}"))
      .Entity<PropertyAuction>(e => e.Kind("Auction").Code(x => x.AuctionNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<AuctionBid>(e => e.Kind("Auction bid").Code(x => x.Auction == null ? null : x.Auction.AuctionNo.Value)
        .Under<Property>(x => x.Auction == null ? null : x.Auction.PropertyId))

      .Entity<PropertyBoundary>(e => e.Kind("Boundary survey").Under<Property>(x => x.PropertyId)
        .Label(x => x.SurveyDate is { } on ? $"surveyed {on:yyyy-MM-dd}" : null))
      .Skip<BoundaryPoint>()
      .Skip<EncroachmentBoundaryPoint>()
      .Entity<PropertyEncroachment>(e => e.Kind(Encroachment).Code(x => x.EncroachmentNo.Value).Label(x => x.EncroachmentNo.Value)
        .Under<Property>(x => x.PropertyId))
      .Entity<PropertyLitigation>(e => e.Kind(CourtCase).Code(x => x.CaseNo).Label(x => x.CaseTitle).Under<Property>(x => x.PropertyId))
      .Entity<LitigationParty>(e => e.Kind(CaseParty).Code(x => x.Litigation == null ? null : x.Litigation.CaseNo).Label(x => x.PartyName)
        .Under<Property>(x => x.Litigation == null ? null : x.Litigation.PropertyId))
      .Entity<LitigationHearing>(e => e.Kind(Hearing).Code(x => x.Litigation == null ? null : x.Litigation.CaseNo).Label(x => $"hearing of {x.HearingDate:yyyy-MM-dd}")
        .Under<Property>(x => x.Litigation == null ? null : x.Litigation.PropertyId))
      .Entity<PropertyAppeal>(e => e.Kind(Appeal).Code(x => x.AppealNo.Value).Under<Property>(x => x.PropertyId))
      .Entity<BuildingPlan>(e => e.Kind("Building plan").Code(x => $"{x.PlanNo.Value} rev {x.RevisionNo}").Under<Property>(x => x.PropertyId))

      // a property's paper hangs off the property; an owner's own paper off the owner
      .Entity<PropertyDocument>(e => e.Kind("Document").Label(x => x.Title ?? x.OriginalFileName)
        .Under<Property>(x => x.PropertyId is { } property ? property : (object)OwnerId.Of(x.EntityId)))

      .Entity<PropertyAttributeValue>(e => e.Kind("Custom field value").Under<Property>(x => x.PropertyId))
      .Entity<AttributeDefinition>(e => e.Kind("Custom field").Label(x => x.Label.Value))
      .Entity<CodeSequence>(e => e.Kind("Numbering").Code(x => x.Key.Value).Label(x => x.Pattern))

      // the old change log is read-only now; nothing writes it
      .Skip<PropertyChange>();
  }
}
