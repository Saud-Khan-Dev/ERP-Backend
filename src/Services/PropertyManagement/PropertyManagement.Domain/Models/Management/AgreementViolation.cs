/// One breach of a lease, rent or sale agreement (Act s.28-A).
///
/// Rule 4: exactly one of lease_id / rental_id / transfer_id is set, matching the agreement type.
/// Rule 5: occurrence_no counts violations of the same agreement; the third (or later) one committed
/// within the time given in the previous notice sets led_to_cancellation, and the linked lease or rental
/// is cancelled. Fines may extend to Rs 1,000,000; collecting them is the Tax / Finance Module's job.
public class AgreementViolation : Aggregate<ViolationId>
{
  public const decimal MaxFine = 1_000_000m;
  public const int CancellationOccurrence = 3;

  public PropertyId PropertyId { get; private set; } = default!;
  public MasterId AgreementTypeId { get; private set; } = default!;
  public LeaseId? LeaseId { get; private set; }
  public RentalId? RentalId { get; private set; }
  public TransferId? TransferId { get; private set; }
  public OwnerId ViolatorOwnerId { get; private set; } = default!;
  public DateOnly ViolationDate { get; private set; }
  public string ViolationDescription { get; private set; } = default!;
  /// 1st, 2nd = fine; 3rd within the notice period = cancellation.
  public short OccurrenceNo { get; private set; }
  public string? NoticeNo { get; private set; }
  public DateOnly? NoticeDate { get; private set; }
  /// "The time specified in the notice".
  public DateOnly? NoticeDeadline { get; private set; }
  public decimal? FineAmount { get; private set; }
  /// The authorized officer (Identity user id) who imposed the fine.
  public Guid? FineImposedBy { get; private set; }
  public FineStatus? FineStatus { get; private set; }
  public bool LedToCancellation { get; private set; }
  public DateOnly? CancellationDate { get; private set; }
  public ViolationStatus ViolationStatus { get; private set; }
  public string? Remarks { get; private set; }

  /// The agreement the violation is against — exactly one of the three.
  public sealed record Agreement(PropertyLease? Lease, PropertyRental? Rental, PropertyTransfer? Transfer);

  public sealed record Notice(string? NoticeNo, DateOnly? NoticeDate, DateOnly? NoticeDeadline);

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
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(agreementType);
    ArgumentNullException.ThrowIfNull(agreement);
    ArgumentNullException.ThrowIfNull(violator);
    ArgumentNullException.ThrowIfNull(earlierViolationsOfAgreement);
    property.EnsureActive();
    agreementType.EnsureActive();

    var linked = new object?[] { agreement.Lease, agreement.Rental, agreement.Transfer }.Count(a => a is not null);
    if (linked != 1)
      throw new DomainException("A violation must reference exactly one agreement: a lease, a rental or a sale (transfer).");

    var expected = agreementType.Code.Value switch
    {
      SystemMasterCodes.AgreementLease => agreement.Lease is not null,
      SystemMasterCodes.AgreementRent => agreement.Rental is not null,
      SystemMasterCodes.AgreementSale => agreement.Transfer is not null,
      _ => true
    };

    if (!expected)
      throw new DomainException($"A {agreementType.Name.Value} violation must reference a matching {agreementType.Name.Value.ToLowerInvariant()} agreement.");

    var agreementProperty = agreement.Lease?.PropertyId ?? agreement.Rental?.PropertyId ?? agreement.Transfer!.PropertyId;
    if (agreementProperty != property.Id)
      throw new DomainException("The agreement belongs to another property.");

    var violation = new AgreementViolation
    {
      Id = id,
      PropertyId = property.Id,
      AgreementTypeId = agreementType.Id,
      LeaseId = agreement.Lease?.Id,
      RentalId = agreement.Rental?.Id,
      TransferId = agreement.Transfer?.Id,
      ViolatorOwnerId = violator.Id,
      ViolationDate = violationDate,
      ViolationDescription = Guard.RequiredText(description, 4000, "Violation description"),
      OccurrenceNo = (short)(earlierViolationsOfAgreement.Count + 1),
      ViolationStatus = ViolationStatus.Open,
      Remarks = Guard.Text(remarks, 4000, "Remarks")
    };

    violation.SetNotice(notice);

    // rule 5: third repetition within the time given in the last notice cancels the agreement
    var lastNotice = earlierViolationsOfAgreement
        .Where(v => v.NoticeDeadline.HasValue)
        .OrderByDescending(v => v.ViolationDate)
        .FirstOrDefault();

    var withinNoticePeriod = lastNotice is not null && violationDate <= lastNotice.NoticeDeadline!.Value;

    if (violation.OccurrenceNo >= CancellationOccurrence && withinNoticePeriod && agreementInForce
        && (agreement.Lease is not null || agreement.Rental is not null))
    {
      violation.LedToCancellation = true;
      violation.CancellationDate = violationDate;
      violation.ViolationStatus = ViolationStatus.Cancelled;
    }

    return violation;
  }

  public void IssueNotice(Notice notice)
  {
    EnsureNotRectified();
    SetNotice(notice);
  }

  /// A fine under s.28-A, imposed by the signed-in authorized officer.
  public void ImposeFine(decimal amount, Guid imposedBy)
  {
    // the violation that cancels the agreement can still be fined
    EnsureNotRectified();

    if (amount <= 0 || amount > MaxFine)
      throw new DomainException($"The fine must be greater than zero and may extend to Rs {MaxFine:N0} (s.28-A).");

    if (imposedBy == Guid.Empty)
      throw new DomainException("The imposing officer is required.");

    FineAmount = decimal.Round(amount, 2);
    FineImposedBy = imposedBy;
    FineStatus = global::FineStatus.Imposed;

    if (ViolationStatus == ViolationStatus.Open)
      ViolationStatus = ViolationStatus.Fined;
  }

  /// Paid / waived, or handed over for recovery as arrears of land revenue (s.28(2)).
  public void SetFineStatus(FineStatus status)
  {
    if (FineAmount is null)
      throw new DomainException("No fine has been imposed for this violation.");

    if (!Enum.IsDefined(status) || status == global::FineStatus.Imposed)
      throw new DomainException("A fine can move to Paid, Waived or RecoveryAsArrears.");

    FineStatus = status;
  }

  public void Rectify(string? remarks)
  {
    EnsureNotRectified();

    if (ViolationStatus == ViolationStatus.Cancelled)
      throw new DomainException("This violation already cancelled the agreement; it cannot be marked rectified.");

    ViolationStatus = ViolationStatus.Rectified;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// An appeal (s.32) was filed against the order in this violation.
  public void MarkAppealed()
  {
    if (ViolationStatus == ViolationStatus.Rectified)
      throw new DomainException("A rectified violation cannot be appealed.");

    ViolationStatus = ViolationStatus.Appealed;
  }

  private void SetNotice(Notice notice)
  {
    ArgumentNullException.ThrowIfNull(notice);
    Guard.DateOrder(notice.NoticeDate, notice.NoticeDeadline, "Notice date", "Notice deadline");
    Guard.DateOrder(ViolationDate, notice.NoticeDate, "Violation date", "Notice date");

    NoticeNo = Guard.Text(notice.NoticeNo, 50, "Notice no.");
    NoticeDate = notice.NoticeDate;
    NoticeDeadline = notice.NoticeDeadline;
  }

  private void EnsureNotRectified()
  {
    if (ViolationStatus == ViolationStatus.Rectified)
      throw new DomainException("This violation has been rectified and can no longer change.");
  }
}
