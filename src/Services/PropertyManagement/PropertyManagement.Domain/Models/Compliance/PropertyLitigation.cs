/// One court case (or Dispute Resolution Committee case — Act s.9) about the property, with its parties
/// and hearing log. A departmental appeal to the Chief Secretary is a property_appeal instead; an appeal to
/// a higher court is a new litigation row whose parent_litigation_id is the lower-court case.
public class PropertyLitigation : Aggregate<LitigationId>
{
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
  /// The officer authorized by the DG who filed the complaint (Identity user id) — Act s.30.
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

  public sealed record Related(PropertyEncroachment? Encroachment, PropertyAllotment? Allotment, PropertyLease? Lease, PropertyLitigation? Parent);

  public sealed record Details(
      string CaseTitle,
      LitigationType LitigationType,
      DateOnly? FilingDate,
      GdaRole? GdaRole,
      Guid? FiledByOfficerId,
      string? GdaCounsel,
      string? Remarks);

  public sealed record PartyInput(string PartyName, PropertyOwner? Owner, LitigationPartyRole Role, string? CounselName, string? Remarks);

  public static PropertyLitigation File(
      LitigationId id,
      Property property,
      string caseNo,
      string courtAuthority,
      LitigationStatus status,
      Details details,
      Related related,
      IReadOnlyCollection<PartyInput> parties)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(status);
    ArgumentNullException.ThrowIfNull(related);
    property.EnsureActive();
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Decided))
      throw new DomainException("A new case cannot start decided; record the decision afterwards.");

    var litigation = new PropertyLitigation
    {
      Id = id,
      PropertyId = property.Id,
      CaseNo = Guard.RequiredText(caseNo, 100, "Case no."),
      CourtAuthority = Guard.RequiredText(courtAuthority, 200, "Court / authority"),
      LitigationStatusId = status.Id
    };

    litigation.Apply(details);
    litigation.Link(related);

    foreach (var party in parties ?? Array.Empty<PartyInput>())
      litigation.AddParty(party);

    return litigation;
  }

  public void Update(LitigationStatus currentStatus, Details details)
  {
    EnsureOpen(currentStatus);
    Apply(details);
  }

  public LitigationParty AddParty(PartyInput party)
  {
    ArgumentNullException.ThrowIfNull(party);
    party.Owner?.EnsureActive();

    if (!Enum.IsDefined(party.Role))
      throw new DomainException("Unknown party role.");

    var added = LitigationParty.Create(LitigationPartyId.New(), Id,
      Guard.Text(party.PartyName, 200, "Party name") ?? party.Owner?.OwnerName.Value ?? throw new DomainException("Party name is required."),
      party.Owner?.Id, party.Role, party.CounselName, party.Remarks);

    _parties.Add(added);
    return added;
  }

  /// Logs a hearing; its next date becomes the case's next hearing date.
  public LitigationHearing RecordHearing(LitigationStatus currentStatus, DateOnly hearingDate, string? proceedings, string? orderPassed, DateOnly? nextHearingDate, string? attendedBy)
  {
    EnsureOpen(currentStatus);

    if (FilingDate is { } filed && hearingDate < filed)
      throw new DomainException("A hearing cannot be before the case was filed.");

    if (nextHearingDate is { } next && next <= hearingDate)
      throw new DomainException("The next hearing must be after this one.");

    var hearing = LitigationHearing.Create(LitigationHearingId.New(), Id, hearingDate, proceedings, orderPassed, nextHearingDate, attendedBy);
    _hearings.Add(hearing);
    NextHearingDate = nextHearingDate;
    return hearing;
  }

  /// Pending, Withdrawn, Settled ... Deciding has its own step.
  public void ChangeStatus(LitigationStatus currentStatus, LitigationStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureOpen(currentStatus);
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Decided) || status.Is(SystemMasterCodes.Appealed))
      throw new DomainException("Use decide, or file the appeal as a new case, for these statuses.");

    LitigationStatusId = status.Id;
  }

  public void Decide(LitigationStatus currentStatus, LitigationStatus decidedStatus, DateOnly decisionDate, string outcome)
  {
    EnsureOpen(currentStatus);
    decidedStatus.EnsureIs(SystemMasterCodes.Decided);

    if (FilingDate is { } filed && decisionDate < filed)
      throw new DomainException("A case cannot be decided before it was filed.");

    LitigationStatusId = decidedStatus.Id;
    DecisionDate = decisionDate;
    DecisionOutcome = Guard.RequiredText(outcome, 4000, "Decision outcome");
    NextHearingDate = null;
  }

  /// A higher-court case was filed against this one.
  public void MarkAppealed(LitigationStatus appealedStatus, string appealedTo)
  {
    appealedStatus.EnsureIs(SystemMasterCodes.Appealed);
    LitigationStatusId = appealedStatus.Id;
    AppealedTo = Guard.RequiredText(appealedTo, 200, "Appealed to");
  }

  private void Link(Related related)
  {
    if (related.Encroachment is { } encroachment && encroachment.PropertyId != PropertyId)
      throw new DomainException("The related encroachment belongs to another property.");

    if (related.Allotment is { } allotment && allotment.PropertyId != PropertyId)
      throw new DomainException("The related allotment belongs to another property.");

    if (related.Lease is { } lease && lease.PropertyId != PropertyId)
      throw new DomainException("The related lease belongs to another property.");

    if (related.Parent is { } parent && parent.PropertyId != PropertyId)
      throw new DomainException("The lower-court case belongs to another property.");

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
  }

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(details.LitigationType);

    if (details.LitigationType.Id != LitigationTypeId)
      details.LitigationType.EnsureActive();

    if (details.GdaRole is { } role && !Enum.IsDefined(role))
      throw new DomainException("Unknown GDA role.");

    // Act s.30: only an officer authorized by the DG may file a complaint in court
    if (details.LitigationType.Is(SystemMasterCodes.LitigationCriminalComplaint)
        && (details.FiledByOfficerId is null || details.FiledByOfficerId == Guid.Empty))
      throw new DomainException("A criminal complaint must record the authorized officer who filed it (Act s.30).");

    CaseTitle = Guard.RequiredText(details.CaseTitle, 300, "Case title");
    LitigationTypeId = details.LitigationType.Id;
    FilingDate = details.FilingDate;
    GdaRole = details.GdaRole;
    FiledByOfficerId = details.FiledByOfficerId;
    GdaCounsel = Guard.Text(details.GdaCounsel, 150, "GDA counsel");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }
}

public class LitigationParty : Entity<LitigationPartyId>
{
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
    CounselName = Guard.Text(counselName, 150, "Counsel name"),
    Remarks = Guard.Text(remarks, 300, "Remarks")
  };
}

/// One hearing and the order passed in it.
public class LitigationHearing : Entity<LitigationHearingId>
{
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
    Proceedings = Guard.Text(proceedings, 4000, "Proceedings"),
    OrderPassed = Guard.Text(orderPassed, 4000, "Order passed"),
    NextHearingDate = nextHearingDate,
    AttendedBy = Guard.Text(attendedBy, 150, "Attended by")
  };
}
