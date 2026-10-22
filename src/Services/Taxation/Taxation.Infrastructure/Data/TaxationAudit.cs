/// How Taxation's records read in the audit trail (BuildingBlock/Audit). Every record a tax file holds - its Property
/// facts, payers, head values, overrides, holds, agreements, demands, notices, surcharges, arrears, payments, fees and
/// clearances - hangs under the file, so the file's History lists it; a rate set's factors and rates hang under the rate
/// set, a circle's towns and posts under the circle, a notice table's rows and columns under the table.
///
/// Not written one by one: the lines of a demand (a run writes them in bulk), the pieces of a payment (its allocations),
/// the reference syncs and SMS queued after an act (logs of their own), and the daily job's run row - the acts that write
/// them record their own line ("Ran", "Approved", "Received" ...).
public static class TaxationAudit
{
  public const string Module = "Taxation";

  public static void Configure(AuditProfile profile)
  {
    profile
      .Skip<DemandLine>()
      .Skip<PaymentAllocation>()
      .Skip<PsidSync>()
      .Skip<SmsMessage>()
      .Skip<JobRun>()
      .Skip<AuditLog>()
      // the notice's frozen print and its own edit list: an edit is written as its own line, with the reason
      .Ignore("Particulars", "QrPayload", "EditedFields", "Variables", "Columns")
      // who did it is the line's own actor; the user ids kept on the record would only repeat it as a number
      .Ignore("IssuedBy", "ConsumedBy", "RevokedBy", "ReversedBy", "ReceivedBy", "ApprovedBy", "ComputedBy", "ConfirmedBy", "CancelledBy", "EnteredBy",
        "StartedBy");

    // ---- tax files and everything under them ----
    profile.Entity<TaxFile>(e => e.Kind("Tax file").Code(f => f.FileNo).Label(f => f.PropertyCode)
      .Field("Psid", "1Link reference").Field("TaxCategory", "Category").Field("TaxStatus", "Status").Field("CircleId", "Circle")
      .Ignore("PropertyId", "TownId"));
    profile.Entity<TaxFileBasis>(e => e.Kind("Property facts").Under<TaxFile>(b => b.TaxFileId));
    profile.Entity<TaxFilePayer>(e => e.Kind("Payer").Label(p => p.Name).Under<TaxFile>(p => p.TaxFileId).Field("IsAddressee", "Addressee"));
    profile.Entity<TaxFileHeadValue>(e => e.Kind("Head value").Under<TaxFile>(v => v.TaxFileId).Field("HeadId", "Head"));
    profile.Entity<TaxFileOverride>(e => e.Kind("Override").Under<TaxFile>(o => o.TaxFileId).Field("HeadId", "Head").Field("PolicyId", "Special policy"));
    profile.Entity<LitigationHold>(e => e.Kind("Litigation hold").Label(h => h.Reference).Under<TaxFile>(h => h.TaxFileId).Field("EndedOn", "Last day"));
    profile.Entity<AgreementSchedule>(e => e.Kind("Agreement").Code(a => a.SourceNo).Under<TaxFile>(a => a.TaxFileId));
    profile.Entity<AgreementStepRevision>(e => e.Kind("Agreement step revision").Under<TaxFile>(r => r.TaxFileId).Field("ScheduleId", "Agreement"));
    profile.Entity<Demand>(e => e.Kind("Demand").Code(d => d.DemandNo).Label(d => d.PayerName).Under<TaxFile>(d => d.TaxFileId)
      .Field("ArrearsBf", "Arrears brought forward").Field("RunId", "Demand run").Field("BasisId", "Property facts").Field("PayerId", "Payer"));
    profile.Entity<Arrears>(e => e.Kind("Arrears").Under<TaxFile>(a => a.TaxFileId).Field("HeadId", "Head").Field("AddedToDemandId", "Billed on demand"));
    profile.Entity<Penalty>(e => e.Kind("Surcharge").Under<TaxFile>(p => p.TaxFileId).Field("RunId", "Surcharge run").Field("RuleId", "Surcharge rule"));
    profile.Entity<Notice>(e => e.Kind("Notice").Code(n => n.EndstNo ?? n.NoticeNo).Label(n => n.NoticeNo).Under<TaxFile>(n => n.TaxFileId)
      .Field("EndstNo", "Endorsement no.").Field("Psid", "1Link reference").Field("ServedByEmployeeId", "Served by (employee)"));
    profile.Entity<Payment>(e => e.Kind("Payment").Code(p => p.ReceiptNo ?? p.TransactionRef).Label(p => p.PayerName).Under<TaxFile>(p => p.TaxFileId)
      .Field("Psid", "1Link reference"));
    profile.Entity<Fee>(e => e.Kind("Fee").Code(f => f.FeeNo).Under<TaxFile>(f => f.TaxFileId).Field("HeadId", "Head").Field("ExemptionReason", "Waiver or exemption reason"));
    profile.Entity<Clearance>(e => e.Kind("Clearance").Code(c => c.ClearanceNo).Label(c => c.PayerName).Under<TaxFile>(c => c.TaxFileId));
    profile.Entity<Party>(e => e.Kind("Party").Label(p => p.Name).Field("CnicNtn", "CNIC / NTN"));

    // ---- runs ----
    profile.Entity<DemandRun>(e => e.Kind("Demand run").Code(r => "FY " + FiscalYear.Label(r.FiscalYear)).Field("RateSetId", "Rate set").Field("CircleId", "Circle"));
    profile.Entity<PenaltyRun>(e => e.Kind("Surcharge run").Code(r => "FY " + FiscalYear.Label(r.FiscalYear)).Label(r => r.Reference).Field("RuleId", "Surcharge rule").Field("CircleId", "Circle"));

    // ---- configuration ----
    profile.Entity<TaxCircle>(e => e.Kind("Circle"));
    profile.Entity<TaxCircleTown>(e => e.Kind("Town in circle").Code(t => t.TownCode).Under<TaxCircle>(t => t.CircleId).Ignore("TownId"));
    profile.Entity<TaxCircleStaff>(e => e.Kind("Circle post").Under<TaxCircle>(s => s.CircleId));
    profile.Entity<TaxHead>(e => e.Kind("Tax head"));
    profile.Entity<CategoryMap>(e => e.Kind("Category mapping").Code(m => m.PropertyTypeCode).Ignore("PropertyTypeId"));
    profile.Entity<RateSet>(e => e.Kind("Rate set"));
    profile.Entity<LtfFactor>(e => e.Kind("Locality factor").Under<RateSet>(f => f.RateSetId).Ignore("TownId"));
    profile.Entity<RateBand>(e => e.Kind("Head rate").Under<RateSet>(b => b.RateSetId).Field("HeadId", "Head").Ignore("TownId"));
    profile.Entity<TaxSetting>(e => e.Kind("Setting").Code(s => s.Key));
    profile.Entity<SpecialPolicy>(e => e.Kind("Special policy"));
    profile.Entity<PenaltyRule>(e => e.Kind("Surcharge rule"));
    profile.Entity<MessageTemplate>(e => e.Kind("SMS text"));
    profile.Entity<NoticeTemplate>(e => e.Kind("Notice template").Code(t => EnumText.Words(t.Stage)));
    profile.Entity<NoticeLetterhead>(e => e.Kind("Letterhead"));
    profile.Entity<NoticeTableLayout>(e => e.Kind("Notice table").Label(l => l.BoardReference));
    profile.Entity<NoticeTableRow>(e => e.Kind("Notice table row").Under<NoticeTableLayout>(r => r.LayoutId));
    profile.Entity<NoticeTableColumn>(e => e.Kind("Notice table column").Label(c => c.Heading).Under<NoticeTableLayout>(c => c.LayoutId));
  }
}
