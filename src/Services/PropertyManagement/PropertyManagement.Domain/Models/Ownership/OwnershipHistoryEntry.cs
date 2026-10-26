/// One owner's share in a step of an ownership history.
public sealed record HistoryShare(Guid OwnerId, decimal SharePct);

/// The first step names the first owners on record; every later step is a transfer between owners.
public enum HistoryStepKind { FirstOwners, Transfer }

/// One step of a property's ownership history as the old papers give it, oldest first. FirstOwners: Receivers are the
/// first owners (TransferTypeId, optional, says how they acquired it - first registration, allotment ...). Transfer:
/// Givers pass shares to Receivers (TransferTypeId required). Date is the first day of what is known (Precision).
/// DocumentIds are the papers filed with the entry for this step. Once registered, the step records what it became.
public sealed record HistoryStep(
  HistoryStepKind Kind,
  DateOnly Date,
  DatePrecision Precision,
  Guid? TransferTypeId,
  string? ReferenceNo,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovalOrderRef,
  string? Remarks,
  IReadOnlyList<HistoryShare> Givers,
  IReadOnlyList<HistoryShare> Receivers,
  IReadOnlyList<Guid> DocumentIds,
  Guid? RegisteredTransferId = null,
  IReadOnlyList<Guid>? RegisteredOwnershipIds = null);

/// Everything an ownership history is played against: the property, every ownership row it has (tracked), the owners
/// the steps name, the transfer types they use, the tenure new holders get, the allotment in force and today.
public sealed record HistoryContext(
  Property Property,
  IReadOnlyCollection<PropertyOwnership> Rows,
  IReadOnlyDictionary<OwnerId, PropertyOwner> Owners,
  IReadOnlyDictionary<MasterId, TransferType> TransferTypes,
  TenureType Tenure,
  PropertyAllotment.InForce? AllotmentInForce,
  DateOnly Today);

/// What registering an entry writes: the transfers, the new ownership rows, the present rows that took on their
/// history, and where each step's papers go.
public sealed record HistoryRegistration(
  IReadOnlyList<PropertyTransfer> Transfers,
  IReadOnlyList<PropertyOwnership> NewRows,
  IReadOnlyList<PropertyOwnership> AdoptedRows,
  IReadOnlyList<(Guid DocumentId, DocumentEntityType EntityType, Guid EntityId)> Papers);

/// A property's ownership from the old records - first owner(s), then every transfer up to the present - entered in
/// one go by a clerk and approved ONCE by a second officer (HIS-00001). Until then nothing changes hands. Registering
/// it plays the steps forward through the same rules every transfer passes (shares balance, a giver holds what they
/// give, dates in order, one entry per owner per day), then writes the transfers (COMPLETED, TRF-…) and holdings.
///
/// It fits a property whose ownership has no history yet: nobody holds it (the history ends with its present
/// holders), or its present holders were entered from the register with nothing behind them - then the history must
/// end with exactly them (same owners, shares and start dates), and their holdings take on how they were acquired.
/// People from the old records (no CNIC, deceased) may appear on the way; whoever the history leaves holding a share
/// today needs a CNIC, unless they have died.
public class OwnershipHistoryEntry : Aggregate<HistoryEntryId>
{
  public const int MaxSteps = 60;

  private List<HistoryStep> _steps = new();

  public PropertyId PropertyId { get; private set; } = default!;
  /// HIS-00001, issued by the HISTORY code sequence.
  public BusinessCode EntryNo { get; private set; } = default!;
  public HistoryEntryStatus Status { get; private set; }
  public IReadOnlyList<HistoryStep> Steps
  {
    get => _steps.AsReadOnly();
    private set => _steps = value.ToList();
  }
  public string? Remarks { get; private set; }
  /// Who entered it (CreatedBy holds their user id) and when.
  public string RecordedBy { get; private set; } = default!;
  public DateOnly RecordedOn { get; private set; }
  /// Who registered, rejected or withdrew it, when and (rejected / withdrawn) why.
  public string? DecidedBy { get; private set; }
  public Guid? DecidedByUserId { get; private set; }
  public DateOnly? DecidedOn { get; private set; }
  public string? DecisionReason { get; private set; }

  public bool IsWaiting => Status == HistoryEntryStatus.Waiting;

  public int TransferCount => _steps.Count(s => s.Kind == HistoryStepKind.Transfer);

  /// Enters a history waiting for a second officer: it is played forward now (nothing is written), so an entry that
  /// waits is one that can be registered as it stands.
  public static OwnershipHistoryEntry Record(
      HistoryEntryId id,
      BusinessCode entryNo,
      IReadOnlyList<HistoryStep> steps,
      string? remarks,
      string recordedBy,
      HistoryContext context,
      Approver checkedBy)
  {
    ArgumentNullException.ThrowIfNull(entryNo);
    ArgumentNullException.ThrowIfNull(context);

    var normalized = Normalize(steps);
    Replay(normalized, context, DraftNumbers(normalized), checkedBy, recordedBy, adopt: false);

    return new OwnershipHistoryEntry
    {
      Id = id,
      PropertyId = context.Property.Id,
      EntryNo = entryNo,
      Status = HistoryEntryStatus.Waiting,
      _steps = normalized.ToList(),
      Remarks = Guard.Text(remarks, 4000, "Remarks"),
      RecordedBy = Guard.RequiredText(recordedBy, Approver.NameMaxLength, "Recorded by"),
      RecordedOn = context.Today,
    };
  }

  /// The clerk corrects the steps while it waits; it is played forward again.
  public void Revise(IReadOnlyList<HistoryStep> steps, string? remarks, HistoryContext context, Approver checkedBy)
  {
    ArgumentNullException.ThrowIfNull(context);
    EnsureWaiting("changed");
    EnsureOn(context.Property);

    var normalized = Normalize(steps);
    Replay(normalized, context, DraftNumbers(normalized), checkedBy, RecordedBy, adopt: false);
    _steps = normalized.ToList();
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }

  /// A second officer registers the history: it is played forward against the property as it stands now and written.
  /// transferNos holds one new TRF number per transfer step. Whether the officer who entered it may register it is
  /// the PROPERTY_TRANSFER separation-of-duties rule, which the handler checks first.
  public HistoryRegistration Register(HistoryContext context, IReadOnlyList<BusinessCode> transferNos, Approver approver)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(transferNos);
    ArgumentNullException.ThrowIfNull(approver);
    EnsureWaiting("registered");
    EnsureOn(context.Property);

    if (transferNos.Count != TransferCount)
      throw new InvalidOperationException($"{EntryNo.Value} needs {TransferCount} transfer numbers, got {transferNos.Count}.");

    var result = Replay(_steps, context, transferNos, approver, approver.Name, adopt: true);

    // each step records what it became; its papers go with it
    var papers = new List<(Guid, DocumentEntityType, Guid)>();
    for (var i = 0; i < _steps.Count; i++)
    {
      var (transfer, rows) = result.Steps[i];
      _steps[i] = _steps[i] with { RegisteredTransferId = transfer?.Id.Value, RegisteredOwnershipIds = rows.Select(r => r.Id.Value).ToList() };
      var target = transfer is not null ? (DocumentEntityType.Transfer, transfer.Id.Value) : (DocumentEntityType.Ownership, rows[0].Id.Value);
      papers.AddRange(_steps[i].DocumentIds.Select(d => (d, target.Item1, target.Item2)));
    }

    Status = HistoryEntryStatus.Registered;
    Decide(approver.Name, approver.UserId, context.Today, reason: null);
    return new HistoryRegistration(result.Transfers, result.NewRows, result.AdoptedRows, papers);
  }

  public void Reject(string reason, string rejectedBy, Guid? userId, DateOnly today)
  {
    EnsureWaiting("rejected");
    Status = HistoryEntryStatus.Rejected;
    Decide(rejectedBy, userId, today, Guard.RequiredText(reason, 4000, "Reason"));
  }

  public void Withdraw(string reason, string withdrawnBy, Guid? userId, DateOnly today)
  {
    EnsureWaiting("withdrawn");
    Status = HistoryEntryStatus.Withdrawn;
    Decide(withdrawnBy, userId, today, Guard.RequiredText(reason, 4000, "Reason"));
  }

  /// The papers of every step (filed with the entry while it waits).
  public IEnumerable<Guid> DocumentIds => _steps.SelectMany(s => s.DocumentIds);

  /// A paper (deed, mutation, court order ...) filed with the entry for one step (1 = the first owners). It moves to the
  /// record the step becomes when the entry is registered.
  public void AttachPaper(int stepNo, Guid documentId)
  {
    EnsureWaiting("given papers");
    if (stepNo < 1 || stepNo > _steps.Count)
      throw new DomainException($"{EntryNo.Value} has steps 1 to {_steps.Count}; there is no step {stepNo}.");

    var step = _steps[stepNo - 1];
    if (!step.DocumentIds.Contains(documentId))
      _steps[stepNo - 1] = step with { DocumentIds = step.DocumentIds.Append(documentId).ToList() };
  }

  // =====================================================
  // PLAYING THE STEPS FORWARD
  // =====================================================

  private sealed record Replayed(
    IReadOnlyList<PropertyTransfer> Transfers,
    IReadOnlyList<PropertyOwnership> NewRows,
    IReadOnlyList<PropertyOwnership> AdoptedRows,
    IReadOnlyList<(PropertyTransfer? Transfer, IReadOnlyList<PropertyOwnership> Rows)> Steps);

  /// Plays the steps forward in a sandbox that holds only the history's own rows, through RegisterFirstOwners and
  /// RecordCompleted (every transfer rule), then checks where it ends. adopt = false checks only (nothing that exists
  /// is touched); adopt = true also lets the present holders take on their history.
  private static Replayed Replay(
      IReadOnlyList<HistoryStep> steps,
      HistoryContext context,
      IReadOnlyList<BusinessCode> transferNos,
      Approver approver,
      string actor,
      bool adopt)
  {
    var property = context.Property;
    var code = property.PropertyCode.Value;
    property.EnsureActive();

    var present = context.Rows.Where(r => r.IsCurrent).ToList();
    if (context.Rows.Any(r => !r.IsCurrent))
      throw new DomainException(
        $"{code} already has ownership history in the register (earlier holders, or transfers). Enter any older transfer with \"Record a completed transfer\".");
    if (present.FirstOrDefault(r => r.AcquiredViaTransferId is not null || r.AcquiredViaAllotmentId is not null) is { } explained)
      throw new DomainException(
        $"{NameOf(context, explained.OwnerId)}'s holding of {code} already says how it was acquired (a transfer or an allotment), so the history behind it is in the register.");

    EnsureShape(steps, context);

    var sandbox = new List<PropertyOwnership>();
    var owners = context.Owners.Values.ToList();
    OwnershipRecord Record() => new(property, sandbox, owners, Array.Empty<PropertyTransfer>(), Array.Empty<PropertyEncumbrance>(),
      context.Today, present.Count == 0 ? context.AllotmentInForce : null, replayingHistory: true);

    var results = new List<(PropertyTransfer?, IReadOnlyList<PropertyOwnership>)>();
    var transfers = new List<PropertyTransfer>();

    // step 1: the first owners on record
    var first = steps[0];
    IReadOnlyList<PropertyOwnership> firstRows;
    try
    {
      firstRows = PropertyOwnership.RegisterFirstOwners(
        first.Receivers.Select(r => new PropertyOwnership.FirstOwner(
          context.Owners[OwnerId.Of(r.OwnerId)], context.Tenure, r.SharePct, first.Date, TypeOf(first, context),
          first.ReferenceNo, first.Remarks, first.Precision)).ToList(),
        Record());
    }
    catch (DomainException refused)
    {
      throw new DomainException($"Step 1 (the first owners, {Describe(first)}): {refused.Reason}");
    }
    sandbox.AddRange(firstRows);
    results.Add((null, firstRows));

    // every later step: a transfer between the owners of that moment
    var next = 0;
    for (var i = 1; i < steps.Count; i++)
    {
      var step = steps[i];
      var parties = step.Givers.Select(g => new PropertyTransfer.PartyInput(OwnerId.Of(g.OwnerId), TransferPartyRole.Transferor, g.SharePct))
        .Concat(step.Receivers.Select(r => new PropertyTransfer.PartyInput(OwnerId.Of(r.OwnerId), TransferPartyRole.Transferee, r.SharePct)))
        .ToList();

      PropertyTransfer transfer;
      PropertyTransfer.OwnershipChange change;
      try
      {
        (transfer, change) = PropertyTransfer.RecordCompleted(
          TransferId.New(), transferNos[next++], TypeOf(step, context)!, step.Date, step.ReferenceNo, step.ConsiderationAmount,
          step.Relationship, step.Remarks, parties, approver, step.Date, step.ApprovalOrderRef, noDuesCertificateNo: null, actor,
          Record(), context.Tenure);
      }
      catch (DomainException refused)
      {
        throw new DomainException($"Step {i + 1} ({Describe(step)}): {refused.Reason}");
      }

      transfer.MarkDatePrecision(step.Precision, change.Opened);
      sandbox.AddRange(change.Opened);
      transfers.Add(transfer);
      results.Add((transfer, change.Opened));
    }

    var final = sandbox.Where(r => r.IsCurrent).ToList();

    // nobody holds the property: the history's last holders become its holders, as anyone registered today would
    if (present.Count == 0)
    {
      foreach (var ownerId in final.Select(r => r.OwnerId).Distinct())
        context.Owners[ownerId].EnsureCanHoldToday();
      return new Replayed(transfers, sandbox, Array.Empty<PropertyOwnership>(), results);
    }

    // the property is held: the history must end with exactly its present holders
    var expected = present.OrderBy(r => r.OwnerId.Value).Select(r => (r.OwnerId, r.OwnershipSharePct, r.EffectiveFrom)).ToList();
    var reached = final.OrderBy(r => r.OwnerId.Value).Select(r => (r.OwnerId, r.OwnershipSharePct, r.EffectiveFrom)).ToList();
    if (!expected.SequenceEqual(reached))
      throw new DomainException(
        $"The history must end with {code}'s present owners as the register shows them: "
        + $"{string.Join(", ", present.Select(r => $"{NameOf(context, r.OwnerId)} {r.OwnershipSharePct:0.####}% from {r.EffectiveFrom:yyyy-MM-dd}"))}. "
        + $"As entered, it ends with {string.Join(", ", final.Select(r => $"{NameOf(context, r.OwnerId)} {r.OwnershipSharePct:0.####}% from {r.EffectiveFrom:yyyy-MM-dd}"))}.");

    var newRows = sandbox.Where(r => !r.IsCurrent).ToList();
    var clash = newRows.FirstOrDefault(n => present.Any(p => p.OwnerId == n.OwnerId && p.EffectiveFrom == n.EffectiveFrom));
    if (clash is not null)
      throw new DomainException($"{NameOf(context, clash.OwnerId)} would have two holdings of {code} starting {clash.EffectiveFrom:yyyy-MM-dd}; one entry per owner per day.");

    var adopted = new List<PropertyOwnership>();
    if (adopt)
      foreach (var row in present)
      {
        row.AdoptHistoryOf(final.Single(f => f.OwnerId == row.OwnerId));
        adopted.Add(row);
      }

    // the steps' rows as they end up: a present holder's last row is the existing one, which took on the history
    var steps2 = results
      .Select(s => (s.Item1, (IReadOnlyList<PropertyOwnership>)s.Item2
        .Select(r => r.IsCurrent ? present.Single(p => p.OwnerId == r.OwnerId) : r).ToList()))
      .ToList();
    return new Replayed(transfers, newRows, adopted, steps2);
  }

  /// The order and shape of the steps; the share rules are the transfers' own.
  private static void EnsureShape(IReadOnlyList<HistoryStep> steps, HistoryContext context)
  {
    if (steps.Count == 0)
      throw new DomainException("Enter at least the first owner(s) on record.");
    if (steps.Count > MaxSteps)
      throw new DomainException($"An ownership history holds at most {MaxSteps} steps; enter the rest with \"Record a completed transfer\" once it is registered.");

    for (var i = 0; i < steps.Count; i++)
    {
      var step = steps[i];
      var label = $"Step {i + 1}";

      if (i == 0 && step.Kind != HistoryStepKind.FirstOwners)
        throw new DomainException("The history starts with the first owner(s) on record.");
      if (i > 0 && step.Kind != HistoryStepKind.Transfer)
        throw new DomainException($"{label}: after the first owners, every step is a transfer.");
      if (step.Date > context.Today)
        throw new DomainException($"{label}: a date from the old records cannot be in the future ({step.Date:yyyy-MM-dd}).");
      if (i > 0 && step.Date < steps[i - 1].Date)
        throw new DomainException($"{label} ({Describe(step)}) is earlier than step {i} ({Describe(steps[i - 1])}): enter the steps oldest first.");

      if (step.Receivers.Count == 0)
        throw new DomainException(i == 0 ? "Name the first owner(s) on record, with their shares." : $"{label}: name who received the share.");
      if (i == 0 && step.Givers.Count > 0)
        throw new DomainException("The first owners received the property from nobody on record; a giver starts a transfer step.");
      if (i > 0 && step.Givers.Count == 0)
        throw new DomainException($"{label}: name who gave the share.");
      if (i > 0 && step.TransferTypeId is null)
        throw new DomainException($"{label}: say how the share passed (sale, inheritance, gift ...).");
      if (step.TransferTypeId is { } typeId && !context.TransferTypes.ContainsKey(MasterId.Of(typeId)))
        throw new DomainException($"{label}: the kind of transfer is not one of the register's.");

      foreach (var share in step.Givers.Concat(step.Receivers))
      {
        if (share.SharePct <= 0 || share.SharePct > 100)
          throw new DomainException($"{label}: a share is more than 0% and at most 100%.");
        if (!context.Owners.ContainsKey(OwnerId.Of(share.OwnerId)))
          throw new DomainException($"{label}: an owner it names is not on the owner register.");
      }

      if (step.DocumentIds.Distinct().Count() != step.DocumentIds.Count)
        throw new DomainException($"{label}: a paper is listed twice.");
    }

    if (steps.SelectMany(s => s.DocumentIds).GroupBy(d => d).Any(g => g.Count() > 1))
      throw new DomainException("A paper belongs to one step only.");
  }

  /// A date known only to the month or year is kept as the first day of it (March 1972 → 1972-03-01).
  private static IReadOnlyList<HistoryStep> Normalize(IReadOnlyList<HistoryStep> steps)
  {
    ArgumentNullException.ThrowIfNull(steps);
    return steps.Select(s => s with
    {
      Date = s.Precision switch
      {
        DatePrecision.Month => new DateOnly(s.Date.Year, s.Date.Month, 1),
        DatePrecision.Year => new DateOnly(s.Date.Year, 1, 1),
        _ => s.Date,
      },
      ReferenceNo = Guard.Text(s.ReferenceNo, 100, "Deed / mutation no."),
      Relationship = Guard.Text(s.Relationship, 100, "Relationship"),
      ApprovalOrderRef = Guard.Text(s.ApprovalOrderRef, 100, "Approval order ref"),
      Remarks = Guard.Text(s.Remarks, 4000, "Remarks"),
      Givers = s.Givers ?? Array.Empty<HistoryShare>(),
      Receivers = s.Receivers ?? Array.Empty<HistoryShare>(),
      DocumentIds = s.DocumentIds ?? Array.Empty<Guid>(),
      RegisteredTransferId = null,
      RegisteredOwnershipIds = null,
    }).ToList();
  }

  /// Placeholder numbers for a check run (nothing is written, no number is used up).
  private static IReadOnlyList<BusinessCode> DraftNumbers(IReadOnlyList<HistoryStep> steps) =>
      Enumerable.Range(1, steps.Count(s => s.Kind == HistoryStepKind.Transfer)).Select(n => BusinessCode.Of($"STEP-{n}")).ToList();

  private static TransferType? TypeOf(HistoryStep step, HistoryContext context) =>
      step.TransferTypeId is { } id ? context.TransferTypes[MasterId.Of(id)] : null;

  private static string NameOf(HistoryContext context, OwnerId id) =>
      context.Owners.TryGetValue(id, out var owner) ? owner.OwnerName.Value : "An owner";

  /// "March 1972", "1972", "about 1972", "1972-03-14".
  public static string Describe(HistoryStep step) => FormatDate(step.Date, step.Precision);

  public static string FormatDate(DateOnly date, DatePrecision precision) => precision switch
  {
    DatePrecision.Month => date.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
    DatePrecision.Year => date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
    DatePrecision.About => $"about {date.Year}",
    _ => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
  };

  private void Decide(string by, Guid? userId, DateOnly today, string? reason)
  {
    DecidedBy = Guard.RequiredText(by, Approver.NameMaxLength, "Decided by");
    DecidedByUserId = userId;
    DecidedOn = today;
    DecisionReason = reason;
  }

  private void EnsureWaiting(string act)
  {
    if (!IsWaiting)
      throw new DomainException($"{EntryNo.Value} is {Status.ToString().ToLowerInvariant()}; only a waiting ownership history can be {act}.");
  }

  private void EnsureOn(Property property)
  {
    if (property.Id != PropertyId)
      throw new DomainException("The ownership history belongs to another property.");
  }
}
