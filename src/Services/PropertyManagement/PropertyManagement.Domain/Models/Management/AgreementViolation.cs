/// One breach of a lease, rent, sale or allotment agreement (Act s.28-A).
///
/// Rule 4: exactly one of lease_id / rental_id / transfer_id / allotment_id is set, matching the agreement type, and
/// the violator is a party to that agreement (the lessee, the tenant, a transferee, the allottee).
/// Rule 5: occurrence_no counts this agreement's violations dated on or before this one; the third (or
/// later) one committed inside the period of a notice served on an earlier violation (notice date ≤ violation
/// date ≤ comply-by date) sets led_to_cancellation, and the linked lease or rental is cancelled.
/// Fines may extend to Rs 1,000,000; collecting them is the Tax / Finance Module's job. A fine moves Imposed ->
/// Paid (with the receipt no. and date) or Waived (an approver's decision, with the reason), or Imposed -> recovery as
/// arrears of land revenue (s.28(2)) -> Paid / Waived. Paid and Waived are final.
///
/// An appeal (s.32) against the order puts the violation in APPEALED and remembers the status it had; the
/// decision either sets the violation aside (allowed: RECTIFIED with a remark, any outstanding fine waived,
/// and a lease / rental it cancelled is reinstated) or puts it back where it was (dismissed, remanded,
/// modified, or the appeal withdrawn).
public class AgreementViolation : Aggregate<ViolationId>
{
  public const decimal MaxFine = 1_000_000m;
  public const int CancellationOccurrence = 3;
  public const int NoticeNoMaxLength = 50;
  public const int TextMaxLength = 4000;
  public const string NoDeadlineWarning = "Without a 'comply by' date the third-violation rule cannot apply.";
  public const int ReceiptNoMaxLength = 50;

  public PropertyId PropertyId { get; private set; } = default!;
  public MasterId AgreementTypeId { get; private set; } = default!;
  public LeaseId? LeaseId { get; private set; }
  public RentalId? RentalId { get; private set; }
  public TransferId? TransferId { get; private set; }
  /// A breach of the conditions of an allotment (construction deadline, purpose ...).
  public AllotmentId? AllotmentId { get; private set; }
  public OwnerId ViolatorOwnerId { get; private set; } = default!;
  public DateOnly ViolationDate { get; private set; }
  public string ViolationDescription { get; private set; } = default!;
  /// 1st, 2nd = fine; 3rd within the notice period = cancellation.
  public short OccurrenceNo { get; private set; }
  public string? NoticeNo { get; private set; }
  public DateOnly? NoticeDate { get; private set; }
  /// "The time specified in the notice" (comply-by date).
  public DateOnly? NoticeDeadline { get; private set; }
  public decimal? FineAmount { get; private set; }
  /// The authorized officer (Identity user id) who imposed the fine, and the day.
  public Guid? FineImposedBy { get; private set; }
  public DateOnly? FineImposedOn { get; private set; }
  public FineStatus? FineStatus { get; private set; }
  /// The receipt the payment was made against, and the day it was paid.
  public string? FineReceiptNo { get; private set; }
  public DateOnly? FinePaidOn { get; private set; }
  /// Why the fine was waived (or sent for recovery), who (Identity user id) recorded the last change of the fine, and when.
  public string? FineStatusReason { get; private set; }
  public Guid? FineStatusChangedBy { get; private set; }
  public DateOnly? FineStatusChangedOn { get; private set; }
  public bool LedToCancellation { get; private set; }
  public DateOnly? CancellationDate { get; private set; }
  public ViolationStatus ViolationStatus { get; private set; }
  /// The status the violation had when an appeal was filed against it; restored when the appeal is dismissed
  /// or withdrawn. Null when no appeal is pending.
  public ViolationStatus? PreAppealStatus { get; private set; }
  public string? Remarks { get; private set; }

  /// The agreement the violation is against — exactly one of the four.
  public sealed record Agreement(PropertyLease? Lease, PropertyRental? Rental, PropertyTransfer? Transfer, PropertyAllotment? Allotment = null);

  public sealed record Notice(string? NoticeNo, DateOnly? NoticeDate, DateOnly? NoticeDeadline);

  /// earlierViolationsOfAgreement: the agreement's other violations (any date); only those dated on or before
  /// this one count. agreementInForce: the lease / rental is still in force, so rule 5 can cancel it.
  public static AgreementViolation Record(
      ViolationId id,
      Property property,
      AgreementType agreementType,
      Agreement agreement,
      PropertyOwner violator,
      DateOnly violationDate,
      string description,
      Notice notice,
      IReadOnlyCollection<AgreementViolation> earlierViolationsOfAgreement,
      bool agreementInForce,
      string? remarks,
      DateOnly today,
      out IReadOnlyList<string> warnings)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(agreementType);
    ArgumentNullException.ThrowIfNull(agreement);
    ArgumentNullException.ThrowIfNull(violator);
    ArgumentNullException.ThrowIfNull(earlierViolationsOfAgreement);
    property.EnsureActive();
    agreementType.EnsureActive();

    var linked = new object?[] { agreement.Lease, agreement.Rental, agreement.Transfer, agreement.Allotment }.Count(a => a is not null);
    if (linked != 1)
      throw new DomainException("A violation must reference exactly one agreement: a lease, a rental, a sale (transfer) or an allotment.");

    var expected = agreementType.Code.Value switch
    {
      SystemMasterCodes.AgreementLease => agreement.Lease is not null,
      SystemMasterCodes.AgreementRent => agreement.Rental is not null,
      SystemMasterCodes.AgreementSale => agreement.Transfer is not null,
      SystemMasterCodes.AgreementAllotment => agreement.Allotment is not null,
      _ => true
    };

    if (!expected)
      throw new DomainException($"A {agreementType.Name.Value} violation must reference a matching {agreementType.Name.Value.ToLowerInvariant()} agreement.");

    var agreementProperty = agreement.Lease?.PropertyId ?? agreement.Rental?.PropertyId ?? agreement.Transfer?.PropertyId ?? agreement.Allotment!.PropertyId;
    if (agreementProperty != property.Id)
      throw new DomainException("The agreement belongs to another property.");

    if (violationDate > today)
      throw new DomainException("A violation cannot be dated in the future.");

    var (agreementNo, agreementStart) = agreement.Lease is { } l ? (l.LeaseNo.Value, l.LeaseStartDate)
      : agreement.Rental is { } r ? (r.RentalNo.Value, r.RentalStartDate)
      : agreement.Transfer is { } t ? (t.TransferNo.Value, t.TransferDate)
      : (agreement.Allotment!.AllotmentNo.Value, agreement.Allotment.AllotmentDate);

    // the one who breached it is bound by it: the lessee, the tenant, a transferee, the allottee
    var parties = agreement.Lease is { } byLease ? new[] { byLease.LesseeOwnerId }
      : agreement.Rental is { } byRental ? new[] { byRental.TenantOwnerId }
      : agreement.Transfer is { } byTransfer ? byTransfer.Parties.Where(p => p.PartyRole == TransferPartyRole.Transferee).Select(p => p.OwnerId).ToArray()
      : new[] { agreement.Allotment!.AllotteeOwnerId };

    if (!parties.Contains(violator.Id))
      throw new DomainException(agreement.Transfer is not null
        ? $"{violator.OwnerName.Value} did not receive a share in {agreementNo}; a breach of a sale's conditions is recorded against a transferee."
        : $"{violator.OwnerName.Value} is not the {(agreement.Lease is not null ? "lessee" : agreement.Rental is not null ? "tenant" : "allottee")} of {agreementNo}; record the violation against the party bound by the agreement.");

    if (violationDate < agreementStart)
      throw new DomainException($"The violation date cannot be before {agreementNo} started ({agreementStart:yyyy-MM-dd}).");

    // rule 5 counts this agreement's violations that happened before this one (a late entry of an old breach
    // does not count the newer ones)
    var earlier = earlierViolationsOfAgreement
        .Where(v => v.Id != id && v.IsAgainst(agreement) && v.ViolationDate <= violationDate)
        .ToList();

    var violation = new AgreementViolation
    {
      Id = id,
      PropertyId = property.Id,
      AgreementTypeId = agreementType.Id,
      LeaseId = agreement.Lease?.Id,
      RentalId = agreement.Rental?.Id,
      TransferId = agreement.Transfer?.Id,
      AllotmentId = agreement.Allotment?.Id,
      ViolatorOwnerId = violator.Id,
      ViolationDate = violationDate,
      ViolationDescription = Guard.RequiredText(description, TextMaxLength, "Violation description"),
      OccurrenceNo = (short)(earlier.Count + 1),
      ViolationStatus = ViolationStatus.Open,
      Remarks = Guard.Text(remarks, TextMaxLength, "Remarks")
    };

    warnings = violation.SetNotice(notice, today);

    // rule 5: the third repetition inside the period of a notice served on an earlier violation cancels it
    var withinNoticePeriod = earlier.Any(v => v.NoticeDate is { } served && v.NoticeDeadline is { } deadline
                                              && served <= violationDate && violationDate <= deadline);

    if (violation.OccurrenceNo >= CancellationOccurrence && withinNoticePeriod && agreementInForce
        && (agreement.Lease is not null || agreement.Rental is not null))
    {
      violation.LedToCancellation = true;
      violation.CancellationDate = violationDate;
      violation.ViolationStatus = ViolationStatus.Cancelled;
    }

    return violation;
  }

  /// A violation carries one notice. The warning says when the notice has no comply-by date.
  public IReadOnlyList<string> IssueNotice(Notice notice, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(notice);
    EnsureNotRectified();

    if (NoticeNo is not null || NoticeDate is not null)
      throw new DomainException($"A notice was already issued on {NoticeDate:yyyy-MM-dd}{(NoticeNo is null ? "" : $" ({NoticeNo})")} for this violation. If the breach continues after its deadline, record it as a new violation.");

    if (string.IsNullOrWhiteSpace(notice.NoticeNo))
      throw new DomainException("Notice no. is required.");

    return SetNotice(notice, today);
  }

  /// A fine under s.28-A, imposed by the signed-in authorized officer (their user id) on that day. An unpaid fine can be
  /// corrected by imposing it again; one that was paid, waived or sent for recovery cannot.
  public void ImposeFine(decimal amount, Guid imposedBy, DateOnly? today = null)
  {
    // the violation that cancels the agreement can still be fined
    EnsureNotRectified();

    if (FineStatus is { } current && current != global::FineStatus.Imposed)
      throw new DomainException($"The fine of Rs {FineAmount:N0} is already {DescribeFine(current)}; it can no longer be changed.");

    if (amount <= 0 || amount > MaxFine)
      throw new DomainException($"The fine must be greater than zero and may extend to Rs {MaxFine:N0} (s.28-A).");

    if (imposedBy == Guid.Empty)
      throw new DomainException("The imposing officer is required.");

    FineAmount = decimal.Round(amount, 2);
    FineImposedBy = imposedBy;
    FineImposedOn = today;
    FineStatus = global::FineStatus.Imposed;

    if (ViolationStatus == ViolationStatus.Open)
      ViolationStatus = ViolationStatus.Fined;
  }

  /// What a change of the fine's status records: the receipt no. and day for a payment, the reason for a waiver or a
  /// recovery order.
  public sealed record FineChange(FineStatus Status, string? ReceiptNo = null, DateOnly? PaidOn = null, string? Reason = null);

  /// Paid (receipt no. and date, not in the future), Waived (with the reason) or handed over for recovery as arrears of
  /// land revenue (s.28(2)). Paid and Waived are final. Who may decide which is the handler's business.
  public void ChangeFineStatus(FineChange change, DateOnly today, Guid? recordedBy)
  {
    ArgumentNullException.ThrowIfNull(change);

    if (FineAmount is null || FineStatus is null)
      throw new DomainException("No fine has been imposed for this violation.");

    if (FineStatus is global::FineStatus.Paid or global::FineStatus.Waived)
      throw new DomainException($"The fine of Rs {FineAmount:N0} is already {DescribeFine(FineStatus.Value)}; it can no longer change.");

    switch (change.Status)
    {
      case global::FineStatus.Paid:
        var receipt = Guard.Text(change.ReceiptNo, ReceiptNoMaxLength, "Receipt no.")
          ?? throw new DomainException("Give the receipt no. the fine was paid against.");
        var paidOn = change.PaidOn ?? throw new DomainException("Give the date the fine was paid.");
        if (paidOn > today)
          throw new DomainException($"The payment date cannot be in the future ({paidOn:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");
        if (paidOn < (FineImposedOn ?? ViolationDate))
          throw new DomainException($"The fine cannot have been paid before it was imposed ({FineImposedOn ?? ViolationDate:yyyy-MM-dd}).");
        FineReceiptNo = receipt;
        FinePaidOn = paidOn;
        FineStatusReason = Guard.Text(change.Reason, TextMaxLength, "Remarks");
        break;

      case global::FineStatus.Waived:
        FineStatusReason = Guard.Text(change.Reason, TextMaxLength, "Reason")
          ?? throw new DomainException("Give the reason the fine is waived.");
        break;

      case global::FineStatus.RecoveryAsArrears:
        if (FineStatus == global::FineStatus.RecoveryAsArrears)
          throw new DomainException("The fine has already been sent for recovery as arrears of land revenue.");
        FineStatusReason = Guard.Text(change.Reason, TextMaxLength, "Remarks");
        break;

      default:
        throw new DomainException("A fine can be marked paid, waived, or sent for recovery as arrears of land revenue.");
    }

    FineStatus = change.Status;
    FineStatusChangedBy = recordedBy;
    FineStatusChangedOn = today;
  }

  private static string DescribeFine(FineStatus status) => status switch
  {
    global::FineStatus.Paid => "paid",
    global::FineStatus.Waived => "waived",
    global::FineStatus.RecoveryAsArrears => "sent for recovery as arrears of land revenue",
    _ => "imposed"
  };

  public void Rectify(string? remarks)
  {
    EnsureNotRectified();

    if (LedToCancellation)
      throw new DomainException($"Violation no. {OccurrenceNo} cancelled the agreement on {CancellationDate:yyyy-MM-dd}; it cannot be marked rectified. The cancellation can only be set aside on appeal.");

    if (ViolationStatus == ViolationStatus.Appealed)
      throw new DomainException("This violation is under appeal. Record the appeal's decision, or its withdrawal, first.");

    ViolationStatus = ViolationStatus.Rectified;
    Remarks = Guard.Text(remarks, TextMaxLength, "Remarks") ?? Remarks;
  }

  /// An appeal (s.32) was filed against the order in this violation; its current status is remembered.
  public void MarkAppealed()
  {
    if (ViolationStatus == ViolationStatus.Rectified)
      throw new DomainException("A rectified violation cannot be appealed.");

    if (ViolationStatus == ViolationStatus.Appealed)
      throw new DomainException("This violation is already under appeal. Record that appeal's decision, or withdraw it, first.");

    PreAppealStatus = ViolationStatus;
    ViolationStatus = ViolationStatus.Appealed;
  }

  /// Applies the outcome of a decided or withdrawn appeal against this violation. Allowed: the violation is
  /// set aside (RECTIFIED with a remark; an imposed fine or one in recovery is waived). Otherwise it goes back
  /// to the status it had before the appeal. Returns true when the set-aside violation had cancelled its
  /// lease or rental, which must then be reinstated.
  public bool ApplyAppealOutcome(PropertyAppeal appeal)
  {
    ArgumentNullException.ThrowIfNull(appeal);

    if (!appeal.IsAgainst(AppealOrderSource.AgreementViolation, Id.Value))
      throw new DomainException($"Appeal {appeal.AppealNo.Value} is not against this violation.");

    if (appeal.IsOpen)
      throw new DomainException($"Appeal {appeal.AppealNo.Value} is still open.");

    if (ViolationStatus != ViolationStatus.Appealed)
      return false;

    if (appeal.AppealStatus == AppealStatus.Decided && appeal.DecisionOutcome == AppealOutcome.Allowed)
    {
      ViolationStatus = ViolationStatus.Rectified;
      PreAppealStatus = null;

      if (FineStatus is global::FineStatus.Imposed or global::FineStatus.RecoveryAsArrears)
      {
        FineStatus = global::FineStatus.Waived;
        FineStatusReason = $"Set aside on appeal {appeal.AppealNo.Value}.";
        FineStatusChangedOn = appeal.DecisionDate;
      }

      Remarks = RemarkText.Append(Remarks, $"Set aside on appeal {appeal.AppealNo.Value}.");
      return LedToCancellation;
    }

    var restored = PreAppealStatus ?? ViolationStatus.Open;
    ViolationStatus = restored == ViolationStatus.Open && FineAmount is not null ? ViolationStatus.Fined : restored;
    PreAppealStatus = null;
    return false;
  }

  private bool IsAgainst(Agreement agreement) =>
      (agreement.Lease is not null && LeaseId == agreement.Lease.Id)
      || (agreement.Rental is not null && RentalId == agreement.Rental.Id)
      || (agreement.Transfer is not null && TransferId == agreement.Transfer.Id)
      || (agreement.Allotment is not null && AllotmentId == agreement.Allotment.Id);

  /// Notice no., date and comply-by date go together: a date without its number is refused rather than lost.
  private IReadOnlyList<string> SetNotice(Notice notice, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(notice);

    var noticeNo = Guard.Text(notice.NoticeNo, NoticeNoMaxLength, "Notice no.");

    if (noticeNo is null)
    {
      if (notice.NoticeDate is not null || notice.NoticeDeadline is not null)
        throw new DomainException("A notice date or comply-by date needs its notice no. Enter the notice no., or clear the notice dates.");

      return Array.Empty<string>();
    }

    if (notice.NoticeDate is not { } noticeDate)
      throw new DomainException($"Give the date notice {noticeNo} was issued.");

    if (noticeDate < ViolationDate)
      throw new DomainException($"The notice cannot be dated before the violation ({ViolationDate:yyyy-MM-dd}).");

    if (noticeDate > today)
      throw new DomainException("A notice cannot be dated in the future.");

    if (notice.NoticeDeadline is { } deadline && deadline < noticeDate)
      throw new DomainException("The comply-by date cannot be before the notice date.");

    NoticeNo = noticeNo;
    NoticeDate = noticeDate;
    NoticeDeadline = notice.NoticeDeadline;

    return NoticeDeadline is null ? new[] { NoDeadlineWarning } : Array.Empty<string>();
  }

  private void EnsureNotRectified()
  {
    if (ViolationStatus == ViolationStatus.Rectified)
      throw new DomainException("This violation has been rectified and can no longer change.");
  }
}
