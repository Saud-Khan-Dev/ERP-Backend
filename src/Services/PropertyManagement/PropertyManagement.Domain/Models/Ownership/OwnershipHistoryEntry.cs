/// One owner's share in a step of an ownership history.
public sealed record HistoryShare(Guid OwnerId, decimal SharePct);

/// The first step names the first owners on record; every later step is a transfer between owners.
public enum HistoryStepKind { FirstOwners, Transfer }

/// One step of an ownership history as the old papers give it, oldest first. Date is the first day of what is known
/// (Precision); TransferTypeId says how the share passed (on the first step, optionally, how the first owners got it).
public sealed record HistoryStep(
  HistoryStepKind Kind,
  DateOnly Date,
  DatePrecision Precision,
  Guid? TransferTypeId,
  string? ReferenceNo,
  string? Relationship,
  IReadOnlyList<HistoryShare> Givers,
  IReadOnlyList<HistoryShare> Receivers);

/// What a history is played against: the property, the owners its steps name, their transfer types, the tenure new
/// holders get, the approving officer and today.
public sealed record HistoryContext(
  Property Property,
  IReadOnlyDictionary<OwnerId, PropertyOwner> Owners,
  IReadOnlyDictionary<MasterId, TransferType> TransferTypes,
  TenureType Tenure,
  Approver Approver,
  string RecordedBy,
  DateOnly Today);

/// What registering a history writes: the holdings (every row, ended ones too) and the transfers.
public sealed record HistoryRegistration(IReadOnlyList<PropertyOwnership> Rows, IReadOnlyList<PropertyTransfer> Transfers);

/// A property's ownership from the old records - its first owners, then every transfer up to today - entered in one go by
/// a clerk and registered ONCE by a second officer (HIS-00001). Until then nothing changes hands. It fits only a property
/// nobody has held on the register yet. Registering plays the steps through the same rules every transfer passes and
/// writes them as first owners and completed transfers; whoever it leaves holding a share today needs a CNIC, unless dead.
public class OwnershipHistoryEntry : Aggregate<HistoryEntryId>
{
  public const int MaxSteps = 60;

  private List<HistoryStep> _steps = new();

  public PropertyId PropertyId { get; private set; } = default!;
  /// HIS-00001, issued by the HISTORY code sequence.
  public BusinessCode EntryNo { get; private set; } = default!;
  public HistoryEntryStatus Status { get; private set; }
  /// The steps while the entry waits; once registered, the register holds the truth.
  public IReadOnlyList<HistoryStep> Steps
  {
    get => _steps.AsReadOnly();
    private set => _steps = value.ToList();
  }
  public string? Remarks { get; private set; }
  public string RecordedBy { get; private set; } = default!;
  /// Who last changed the steps (the first entry, or a revision): neither may register it.
  public Guid? LastEditedByUserId { get; private set; }
  public string? DecidedBy { get; private set; }
  public Guid? DecidedByUserId { get; private set; }
  public DateOnly? DecidedOn { get; private set; }
  public string? DecisionReason { get; private set; }

  public bool IsWaiting => Status == HistoryEntryStatus.Waiting;

  /// Enters a history waiting for a second officer. It is played through now (nothing is written), so an entry that
  /// waits is one that can be registered as it stands.
  public static OwnershipHistoryEntry Record(HistoryEntryId id, BusinessCode entryNo, IReadOnlyList<HistoryStep> steps, string? remarks,
      Guid? editedBy, HistoryContext context, bool propertyHeldBefore)
  {
    ArgumentNullException.ThrowIfNull(entryNo);
    ArgumentNullException.ThrowIfNull(context);
    EnsureFits(context, propertyHeldBefore);
    var normalized = HistoryPlayer.Normalize(steps);
    HistoryPlayer.Play(normalized, context, n => BusinessCode.Of($"STEP-{n}"), id);

    return new OwnershipHistoryEntry
    {
      Id = id,
      PropertyId = context.Property.Id,
      EntryNo = entryNo,
      Status = HistoryEntryStatus.Waiting,
      _steps = normalized.ToList(),
      Remarks = Guard.Text(remarks, 4000, "Remarks"),
      RecordedBy = Guard.RequiredText(context.RecordedBy, Approver.NameMaxLength, "Recorded by"),
      LastEditedByUserId = editedBy,
    };
  }

  /// Puts a waiting or rejected entry right; it waits for a second officer again.
  public void Revise(IReadOnlyList<HistoryStep> steps, string? remarks, Guid? editedBy, HistoryContext context, bool propertyHeldBefore)
  {
    ArgumentNullException.ThrowIfNull(context);
    EnsureOn(context.Property);
    if (Status is not (HistoryEntryStatus.Waiting or HistoryEntryStatus.Rejected))
      throw new DomainException($"{EntryNo.Value} is {Status.ToString().ToLowerInvariant()}; only a waiting or rejected history is put right.");
    EnsureFits(context, propertyHeldBefore);
    var normalized = HistoryPlayer.Normalize(steps);
    HistoryPlayer.Play(normalized, context, n => BusinessCode.Of($"STEP-{n}"), Id);

    _steps = normalized.ToList();
    Remarks = Guard.Text(remarks, 4000, "Remarks");
    LastEditedByUserId = editedBy;
    Status = HistoryEntryStatus.Waiting;
    DecidedBy = null;
    DecidedByUserId = null;
    DecidedOn = null;
    DecisionReason = null;
  }

  /// The second officer registers it: first owners and completed transfers, numbered by `number`.
  public HistoryRegistration Register(HistoryContext context, bool propertyHeldBefore, Func<int, BusinessCode> number)
  {
    ArgumentNullException.ThrowIfNull(context);
    EnsureOn(context.Property);
    EnsureWaiting("registered");
    EnsureFits(context, propertyHeldBefore);
    var result = HistoryPlayer.Play(_steps, context, number, Id);
    Decide(HistoryEntryStatus.Registered, context.Approver, context.Today, null);
    return result;
  }

  /// Sent back with the reason; the clerk may put it right and send it again.
  public void Reject(Approver by, DateOnly today, string reason)
  {
    EnsureWaiting("rejected");
    Decide(HistoryEntryStatus.Rejected, by, today, Guard.RequiredText(reason, 1000, "Why it is rejected"));
  }

  public void Withdraw(Approver by, DateOnly today, string reason)
  {
    if (Status is not (HistoryEntryStatus.Waiting or HistoryEntryStatus.Rejected))
      throw new DomainException($"{EntryNo.Value} is {Status.ToString().ToLowerInvariant()} and can no longer be withdrawn.");
    Decide(HistoryEntryStatus.Withdrawn, by, today, Guard.RequiredText(reason, 1000, "Why it is withdrawn"));
  }

  private static void EnsureFits(HistoryContext context, bool propertyHeldBefore)
  {
    context.Property.EnsureActive();
    if (propertyHeldBefore)
      throw new DomainException(
        $"{context.Property.PropertyCode.Value} already has owners on the register. Its history from the old records is entered only while nobody is on record.");
  }

  private void Decide(HistoryEntryStatus status, Approver by, DateOnly today, string? reason)
  {
    ArgumentNullException.ThrowIfNull(by);
    Status = status;
    DecidedBy = Guard.RequiredText(by.Name, Approver.NameMaxLength, "Decided by");
    DecidedByUserId = by.UserId;
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

/// Plays an ownership history forward through the register's own rules, in memory.
public static class HistoryPlayer
{
  /// A month or year is kept as its first day; a vague date never falls before the step it follows (the order of the
  /// steps decides), while an exact one must come after it.
  public static IReadOnlyList<HistoryStep> Normalize(IReadOnlyList<HistoryStep> steps)
  {
    ArgumentNullException.ThrowIfNull(steps);
    if (steps.Count == 0)
      throw new DomainException("Give the first owners on record, then each transfer.");
    if (steps.Count > OwnershipHistoryEntry.MaxSteps)
      throw new DomainException($"An ownership history has at most {OwnershipHistoryEntry.MaxSteps} steps.");

    var result = new List<HistoryStep>();
    DateOnly? previous = null;
    for (var i = 0; i < steps.Count; i++)
    {
      var s = steps[i];
      var label = $"Step {i + 1}";
      if (i == 0 && s.Kind != HistoryStepKind.FirstOwners)
        throw new DomainException("The first step names the first owners on record.");
      if (i > 0 && s.Kind != HistoryStepKind.Transfer)
        throw new DomainException($"{label}: after the first owners, every step is a transfer.");
      if ((s.Receivers ?? []).Count == 0)
        throw new DomainException(i == 0 ? "Name the first owners on record, with their shares." : $"{label}: name who received the share.");
      if (i == 0 && (s.Givers ?? []).Count > 0)
        throw new DomainException("The first owners received the property from nobody on record.");
      if (i > 0 && (s.Givers ?? []).Count == 0)
        throw new DomainException($"{label}: name who gave the share.");
      if (i > 0 && s.TransferTypeId is null)
        throw new DomainException($"{label}: say how the share passed (sale, inheritance, gift ...).");

      var date = s.Precision switch
      {
        DatePrecision.Month => new DateOnly(s.Date.Year, s.Date.Month, 1),
        DatePrecision.Year or DatePrecision.About => new DateOnly(s.Date.Year, 1, 1),
        _ => s.Date,
      };
      if (previous is { } before && date <= before)
      {
        if (s.Precision == DatePrecision.Exact)
          throw new DomainException($"{label} ({date:dd'/'MM'/'yyyy}) is not after the step before it: enter the steps oldest first.");
        date = before.AddDays(1);
      }
      previous = date;
      result.Add(s with
      {
        Date = date,
        ReferenceNo = Guard.Text(s.ReferenceNo, 100, "Deed / mutation no."),
        Relationship = Guard.Text(s.Relationship, 100, "Relationship"),
        Givers = s.Givers ?? [],
        Receivers = s.Receivers ?? [],
      });
    }
    return result;
  }

  public static HistoryRegistration Play(IReadOnlyList<HistoryStep> steps, HistoryContext context, Func<int, BusinessCode> number, HistoryEntryId entry)
  {
    var rows = new List<PropertyOwnership>();
    var transfers = new List<PropertyTransfer>();
    var owners = context.Owners.Values.ToList();
    OwnershipRecord Record() => new(context.Property, rows, owners, [], [], context.Today, fromOldRecords: true);
    PropertyOwner Owner(Guid id, string label) =>
        context.Owners.TryGetValue(OwnerId.Of(id), out var owner) ? owner : throw new DomainException($"{label}: an owner it names is not on the owner register.");
    TransferType? TypeOf(HistoryStep step, string label) =>
        step.TransferTypeId is not { } id ? null
        : context.TransferTypes.TryGetValue(MasterId.Of(id), out var type) ? type
        : throw new DomainException($"{label}: the kind of transfer is not one of the register's.");

    for (var i = 0; i < steps.Count; i++)
    {
      var step = steps[i];
      var label = $"Step {i + 1}";
      try
      {
        if (step.Kind == HistoryStepKind.FirstOwners)
        {
          var first = step.Receivers.Select(r => new PropertyOwnership.FirstOwner(Owner(r.OwnerId, label), context.Tenure, r.SharePct, step.Date,
            TypeOf(step, label), step.ReferenceNo, Precision: step.Precision)).ToList();
          rows.AddRange(PropertyOwnership.RegisterFirstOwners(first, Record()));
          continue;
        }

        var parties = step.Givers.Select(g => new PropertyTransfer.PartyInput(Owner(g.OwnerId, label).Id, TransferPartyRole.Transferor, g.SharePct))
          .Concat(step.Receivers.Select(r => new PropertyTransfer.PartyInput(Owner(r.OwnerId, label).Id, TransferPartyRole.Transferee, r.SharePct)))
          .ToList();
        var (transfer, change) = PropertyTransfer.RecordCompleted(
          TransferId.New(), number(transfers.Count + 1), TypeOf(step, label)!, step.Date, step.ReferenceNo, null, step.Relationship,
          $"From the old records ({context.RecordedBy}).", parties, context.Approver, context.Today, null, null, context.Approver.Name,
          Record(), context.Tenure, precision: step.Precision);
        transfer.FromHistory(entry, step.Precision);
        transfers.Add(transfer);
        rows.AddRange(change.Opened);
      }
      catch (DomainException error) when (!error.Reason.StartsWith("Step ", StringComparison.Ordinal))
      {
        throw new DomainException($"{label}: {error.Reason}");
      }
    }

    // whoever the history leaves holding a share today needs a CNIC (a dead holder's estate excepted)
    foreach (var holder in rows.Where(r => r.IsCurrent).Select(r => context.Owners[r.OwnerId]).Distinct())
      holder.EnsureCanHoldToday();

    return new HistoryRegistration(rows, transfers);
  }
}
