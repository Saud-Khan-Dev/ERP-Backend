/// One court case (or Dispute Resolution Committee case — Act s.9) about the property, with its parties
/// and hearing log. A departmental appeal to the Chief Secretary is a property_appeal instead; an appeal to
/// a higher court is a new litigation row whose parent_litigation_id is the lower-court case.
///
/// A case is open until it is decided, appealed (the matter continues in the higher-court case), withdrawn,
/// settled or closed (ClosedStatusCodes). A closed case takes no hearings, parties, decision or edits.
public class PropertyLitigation : Aggregate<LitigationId>
{
  public const int CaseNoMaxLength = 100;
  public const int CourtAuthorityMaxLength = 200;
  public const int CaseTitleMaxLength = 300;
  public const int CounselMaxLength = 150;
  public const int TextMaxLength = 4000;

  /// The litigation statuses that close a case — the one shared definition (register filters and the
  /// legal-matters list read it too). Any other status (PENDING, or one an admin adds) is a case still running.
  public static readonly IReadOnlyList<string> ClosedStatusCodes = new[]
  {
    SystemMasterCodes.Decided, SystemMasterCodes.Appealed, SystemMasterCodes.Withdrawn, SystemMasterCodes.Settled, SystemMasterCodes.Closed
  };

  public static bool IsClosedStatus(LitigationStatus status) => ClosedStatusCodes.Any(status.Is);

  private readonly List<LitigationParty> _parties = new();
  private readonly List<LitigationHearing> _hearings = new();

  public PropertyId PropertyId { get; private set; } = default!;
  public string CaseNo { get; private set; } = default!;
  public string CaseTitle { get; private set; } = default!;
  public string CourtAuthority { get; private set; } = default!;
  public MasterId LitigationTypeId { get; private set; } = default!;
  public MasterId LitigationStatusId { get; private set; } = default!;
  public DateOnly? FilingDate { get; private set; }
  public GdaRole? GdaRole { get; private set; }
  /// The officer authorized by the DG who filed the complaint (Identity user id) — Act s.30. Set only when a
  /// criminal complaint is filed (or a case becomes one); never overwritten by later edits.
  public Guid? FiledByOfficerId { get; private set; }
  public EncroachmentId? RelatedEncroachmentId { get; private set; }
  public AllotmentId? RelatedAllotmentId { get; private set; }
  public LeaseId? RelatedLeaseId { get; private set; }
  public DateOnly? NextHearingDate { get; private set; }
  public DateOnly? DecisionDate { get; private set; }
  public string? DecisionOutcome { get; private set; }
  public string? AppealedTo { get; private set; }
  public LitigationId? ParentLitigationId { get; private set; }
  public string? GdaCounsel { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<LitigationParty> Parties => _parties.AsReadOnly();
  public IReadOnlyList<LitigationHearing> Hearings => _hearings.OrderBy(h => h.HearingDate).ToList().AsReadOnly();

  /// Parent: the lower-court case this one appeals, with its current status (it must be decided).
  public sealed record Related(
      PropertyEncroachment? Encroachment,
      PropertyAllotment? Allotment,
      PropertyLease? Lease,
      PropertyLitigation? Parent,
      LitigationStatus? ParentStatus = null);

  public sealed record Details(
      string CaseTitle,
      LitigationType LitigationType,
      DateOnly? FilingDate,
      GdaRole? GdaRole,
      string? GdaCounsel,
      string? Remarks);

  public sealed record PartyInput(string PartyName, PropertyOwner? Owner, LitigationPartyRole Role, string? CounselName, string? Remarks);

  /// filingOfficerId: the signed-in authorized officer; required (and recorded) only for a criminal complaint.
  public static PropertyLitigation File(
      LitigationId id,
      Property property,
      string caseNo,
      string courtAuthority,
      LitigationStatus status,
      Details details,
      Related related,
      IReadOnlyCollection<PartyInput> parties,
      Guid? filingOfficerId = null)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(status);
    ArgumentNullException.ThrowIfNull(related);
    ArgumentNullException.ThrowIfNull(details);
    property.EnsureActive();
    status.EnsureActive();

    if (IsClosedStatus(status))
      throw new DomainException($"A new case cannot start as {status.Name.Value}. File it as pending, then record its decision, withdrawal or settlement.");

    var litigation = new PropertyLitigation
    {
      Id = id,
      PropertyId = property.Id,
      CaseNo = Guard.RequiredText(caseNo, CaseNoMaxLength, "Case no."),
      CourtAuthority = Guard.RequiredText(courtAuthority, CourtAuthorityMaxLength, "Court / authority"),
      LitigationStatusId = status.Id
    };

    if (details.LitigationType.Is(SystemMasterCodes.LitigationCriminalComplaint))
      litigation.FiledByOfficerId = RequireOfficer(filingOfficerId);

    litigation.Apply(details);
    litigation.Link(related);

    foreach (var party in parties ?? Array.Empty<PartyInput>())
      litigation.AddPartyCore(party);

    return litigation;
  }

  /// True when saving these details would make the case a criminal complaint it was not before — only then
  /// is the signed-in authorized officer needed (Act s.30).
  public bool BecomesCriminalComplaint(LitigationType type)
  {
    ArgumentNullException.ThrowIfNull(type);
    return type.Is(SystemMasterCodes.LitigationCriminalComplaint) && type.Id != LitigationTypeId;
  }

  /// officerId: the signed-in authorized officer; used only when the edit turns the case into a criminal
  /// complaint. The officer who filed an existing complaint is never overwritten.
  public void Update(LitigationStatus currentStatus, Details details, Guid? officerId = null)
  {
    ArgumentNullException.ThrowIfNull(details);
    EnsureOpen(currentStatus);

    if (BecomesCriminalComplaint(details.LitigationType))
      FiledByOfficerId = RequireOfficer(officerId);

    Apply(details);
  }

  public LitigationParty AddParty(LitigationStatus currentStatus, PartyInput party)
  {
    EnsureOpen(currentStatus);
    return AddPartyCore(party);
  }

  /// Logs a hearing; its next date becomes the case's next hearing date.
  public LitigationHearing RecordHearing(LitigationStatus currentStatus, DateOnly hearingDate, string? proceedings, string? orderPassed, DateOnly? nextHearingDate, string? attendedBy)
  {
    EnsureOpen(currentStatus);

    if (FilingDate is { } filed && hearingDate < filed)
      throw new DomainException($"A hearing cannot be before the case was filed ({filed:yyyy-MM-dd}).");

    if (nextHearingDate is { } next && next <= hearingDate)
      throw new DomainException("The next hearing must be after this one.");

    var hearing = LitigationHearing.Create(LitigationHearingId.New(), Id, hearingDate, proceedings, orderPassed, nextHearingDate, attendedBy);
    _hearings.Add(hearing);
    NextHearingDate = nextHearingDate;
    return hearing;
  }

  /// Pending, Withdrawn, Settled, Closed ... Deciding has its own step, and an appeal is filed as a new case.
  /// Withdrawing, settling or closing ends the case, so it no longer has a next hearing.
  public void ChangeStatus(LitigationStatus currentStatus, LitigationStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureOpen(currentStatus);
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Decided))
      throw new DomainException("Use decide, so the decision date and outcome are recorded.");

    if (status.Is(SystemMasterCodes.Appealed))
      throw new DomainException("An appeal is filed as a new case with this one as its lower-court case; that marks this case appealed.");

    if (status.Id == LitigationStatusId)
      throw new DomainException($"Case {CaseNo} is already {status.Name.Value}.");

    LitigationStatusId = status.Id;

    if (IsClosedStatus(status))
      NextHearingDate = null;
  }

  public void Decide(LitigationStatus currentStatus, LitigationStatus decidedStatus, DateOnly decisionDate, string outcome)
  {
    EnsureOpen(currentStatus);
    decidedStatus.EnsureIs(SystemMasterCodes.Decided);

    if (FilingDate is { } filed && decisionDate < filed)
      throw new DomainException($"A case cannot be decided before it was filed ({filed:yyyy-MM-dd}).");

    LitigationStatusId = decidedStatus.Id;
    DecisionDate = decisionDate;
    DecisionOutcome = Guard.RequiredText(outcome, TextMaxLength, "Decision outcome");
    NextHearingDate = null;
  }

  /// A higher-court case was filed against this one. Only a decided case can be appealed, and only once.
  public void MarkAppealed(LitigationStatus currentStatus, LitigationStatus appealedStatus, string appealedTo)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    appealedStatus.EnsureIs(SystemMasterCodes.Appealed);
    EnsureAppealable(currentStatus);

    LitigationStatusId = appealedStatus.Id;
    AppealedTo = Guard.RequiredText(appealedTo, CourtAuthorityMaxLength, "Appealed to");
  }

  private void EnsureAppealable(LitigationStatus currentStatus)
  {
    if (currentStatus.Id != LitigationStatusId)
      throw new DomainException("The supplied status does not match the lower-court case.");

    if (currentStatus.Is(SystemMasterCodes.Appealed))
      throw new DomainException($"Case {CaseNo} was already appealed to {AppealedTo}. File a further appeal against that case.");

    if (!currentStatus.Is(SystemMasterCodes.Decided))
      throw new DomainException($"Case {CaseNo} is {currentStatus.Name.Value}; only a decided case can be appealed. Record its decision first.");
  }

  private LitigationParty AddPartyCore(PartyInput party)
  {
    ArgumentNullException.ThrowIfNull(party);
    party.Owner?.EnsureActive();

    if (!Enum.IsDefined(party.Role))
      throw new DomainException("Unknown party role.");

    var added = LitigationParty.Create(LitigationPartyId.New(), Id,
      Guard.Text(party.PartyName, LitigationParty.NameMaxLength, "Party name") ?? party.Owner?.OwnerName.Value ?? throw new DomainException("Party name is required."),
      party.Owner?.Id, party.Role, party.CounselName, party.Remarks);

    _parties.Add(added);
    return added;
  }

  private void Link(Related related)
  {
    if (related.Encroachment is { } encroachment && encroachment.PropertyId != PropertyId)
      throw new DomainException("The related encroachment belongs to another property.");

    if (related.Allotment is { } allotment && allotment.PropertyId != PropertyId)
      throw new DomainException("The related allotment belongs to another property.");

    if (related.Lease is { } lease && lease.PropertyId != PropertyId)
      throw new DomainException("The related lease belongs to another property.");

    if (related.Parent is { } parent)
    {
      if (parent.PropertyId != PropertyId)
        throw new DomainException("The lower-court case belongs to another property.");

      parent.EnsureAppealable(related.ParentStatus ?? throw new ArgumentNullException(nameof(related), "The lower-court case's status is required."));
    }

    RelatedEncroachmentId = related.Encroachment?.Id;
    RelatedAllotmentId = related.Allotment?.Id;
    RelatedLeaseId = related.Lease?.Id;
    ParentLitigationId = related.Parent?.Id;
  }

  private void EnsureOpen(LitigationStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != LitigationStatusId)
      throw new DomainException("The supplied status does not match the case.");

    if (currentStatus.Is(SystemMasterCodes.Decided))
      throw new DomainException($"Case {CaseNo} has been decided. File an appeal as a new case with this one as its parent.");

    if (currentStatus.Is(SystemMasterCodes.Appealed))
      throw new DomainException($"Case {CaseNo} was appealed to {AppealedTo}; record further proceedings on the higher-court case.");

    if (IsClosedStatus(currentStatus))
      throw new DomainException($"Case {CaseNo} is {currentStatus.Name.Value} and closed; it can no longer change.");
  }

  private static Guid RequireOfficer(Guid? officerId) =>
      officerId is { } id && id != Guid.Empty
        ? id
        : throw new DomainException("A criminal complaint must record the authorized officer who filed it (Act s.30).");

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(details.LitigationType);

    if (details.LitigationType.Id != LitigationTypeId)
      details.LitigationType.EnsureActive();

    if (details.GdaRole is { } role && !Enum.IsDefined(role))
      throw new DomainException("Unknown GDA role.");

    CaseTitle = Guard.RequiredText(details.CaseTitle, CaseTitleMaxLength, "Case title");
    LitigationTypeId = details.LitigationType.Id;
    FilingDate = details.FilingDate;
    GdaRole = details.GdaRole;
    GdaCounsel = Guard.Text(details.GdaCounsel, CounselMaxLength, "GDA counsel");
    Remarks = Guard.Text(details.Remarks, TextMaxLength, "Remarks");
  }
}

public class LitigationParty : Entity<LitigationPartyId>
{
  public const int NameMaxLength = 200;
  public const int CounselMaxLength = 150;
  public const int RemarksMaxLength = 300;

  public LitigationId LitigationId { get; private set; } = default!;
  public string PartyName { get; private set; } = default!;
  public OwnerId? PartyOwnerId { get; private set; }
  public LitigationPartyRole PartyRole { get; private set; }
  public string? CounselName { get; private set; }
  public string? Remarks { get; private set; }

  internal static LitigationParty Create(LitigationPartyId id, LitigationId litigationId, string partyName, OwnerId? ownerId, LitigationPartyRole role, string? counselName, string? remarks) => new()
  {
    Id = id,
    LitigationId = litigationId,
    PartyName = partyName,
    PartyOwnerId = ownerId,
    PartyRole = role,
    CounselName = Guard.Text(counselName, CounselMaxLength, "Counsel name"),
    Remarks = Guard.Text(remarks, RemarksMaxLength, "Remarks")
  };
}

/// One hearing and the order passed in it.
public class LitigationHearing : Entity<LitigationHearingId>
{
  public const int AttendedByMaxLength = 150;

  public LitigationId LitigationId { get; private set; } = default!;
  public DateOnly HearingDate { get; private set; }
  public string? Proceedings { get; private set; }
  public string? OrderPassed { get; private set; }
  public DateOnly? NextHearingDate { get; private set; }
  public string? AttendedBy { get; private set; }

  internal static LitigationHearing Create(LitigationHearingId id, LitigationId litigationId, DateOnly hearingDate, string? proceedings, string? orderPassed, DateOnly? nextHearingDate, string? attendedBy) => new()
  {
    Id = id,
    LitigationId = litigationId,
    HearingDate = hearingDate,
    Proceedings = Guard.Text(proceedings, PropertyLitigation.TextMaxLength, "Proceedings"),
    OrderPassed = Guard.Text(orderPassed, PropertyLitigation.TextMaxLength, "Order passed"),
    NextHearingDate = nextHearingDate,
    AttendedBy = Guard.Text(attendedBy, AttendedByMaxLength, "Attended by")
  };
}
