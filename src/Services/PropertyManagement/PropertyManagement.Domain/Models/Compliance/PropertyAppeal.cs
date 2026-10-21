/// A departmental appeal to the Chief Secretary against a GDA order (Act s.32). The decision is final.
///
/// Rule 6: an appeal filed more than 30 days after the order was received gets a warning (not a
/// refusal — the appellate authority decides on condonation); decision_due_date = appeal_date + 120 days.
public class PropertyAppeal : Aggregate<AppealId>
{
  public const int FilingWindowDays = 30;
  public const int DecisionWindowDays = 120;
  public const string DefaultAppellateAuthority = "Chief Secretary, Khyber Pakhtunkhwa";

  public PropertyId PropertyId { get; private set; } = default!;
  /// APL-00001, issued by the APPEAL code sequence.
  public BusinessCode AppealNo { get; private set; } = default!;
  public OwnerId AppellantOwnerId { get; private set; } = default!;
  /// The DG / officer order being appealed.
  public string AppealedOrderRef { get; private set; } = default!;
  public DateOnly AppealedOrderDate { get; private set; }
  public DateOnly? OrderReceivedDate { get; private set; }
  public AppealOrderSource? OrderSourceTable { get; private set; }
  public Guid? OrderSourceId { get; private set; }
  public DateOnly AppealDate { get; private set; }
  /// Chief Secretary, or an officer not below BPS-20 it delegates to (s.32(2)).
  public string AppellateAuthority { get; private set; } = default!;
  /// The officer the appellate authority delegated to (s.32(2)): the name, and their HR record when they are GDA staff
  /// picked from the staff list (a provincial officer has a name only).
  public string? DelegatedOfficer { get; private set; }
  public Guid? DelegatedOfficerEmployeeId { get; private set; }
  public DateOnly DecisionDueDate { get; private set; }
  public DateOnly? DecisionDate { get; private set; }
  public AppealOutcome? DecisionOutcome { get; private set; }
  public string? DecisionDetails { get; private set; }
  public AppealStatus AppealStatus { get; private set; }
  public string? Remarks { get; private set; }

  public sealed record Filing(
      PropertyOwner Appellant,
      string AppealedOrderRef,
      DateOnly AppealedOrderDate,
      DateOnly? OrderReceivedDate,
      AppealOrderSource? OrderSourceTable,
      Guid? OrderSourceId,
      DateOnly AppealDate,
      string? AppellateAuthority,
      string? DelegatedOfficer,
      string? Remarks,
      Guid? DelegatedOfficerEmployeeId = null);

  /// otherAppeals: the property's other appeals. One order is appealed once: a second appeal against the same
  /// order is refused while the first is open, and after it was decided (the decision is final). A withdrawn
  /// appeal does not block a new one.
  public static PropertyAppeal File(
      AppealId id,
      Property property,
      BusinessCode appealNo,
      Filing filing,
      IReadOnlyCollection<PropertyAppeal> otherAppeals,
      out IReadOnlyList<string> warnings)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(appealNo);
    ArgumentNullException.ThrowIfNull(otherAppeals);
    property.EnsureActive();

    var appeal = new PropertyAppeal { Id = id, PropertyId = property.Id, AppealNo = appealNo, AppealStatus = AppealStatus.Filed };
    warnings = appeal.Apply(filing);
    appeal.EnsureOnlyAppealAgainstOrder(otherAppeals);
    return appeal;
  }

  /// The appealed record (order source) is fixed once filed: changing it would leave the first record marked
  /// as under appeal. The order reference, dates and authority can be corrected.
  public IReadOnlyList<string> Update(Filing filing, IReadOnlyCollection<PropertyAppeal> otherAppeals)
  {
    ArgumentNullException.ThrowIfNull(filing);
    ArgumentNullException.ThrowIfNull(otherAppeals);

    if (AppealStatus != AppealStatus.Filed)
      throw new DomainException($"Appeal {AppealNo.Value} is {Describe(AppealStatus)} and can no longer be edited.");

    if (filing.OrderSourceTable != OrderSourceTable || filing.OrderSourceId != OrderSourceId)
      throw new DomainException($"The appealed record of appeal {AppealNo.Value} cannot be changed. Withdraw this appeal and file a new one against the other order.");

    var warnings = Apply(filing);
    EnsureOnlyAppealAgainstOrder(otherAppeals);
    return warnings;
  }

  public bool IsOpen => AppealStatus is AppealStatus.Filed or AppealStatus.UnderHearing;

  /// The appeal is against this record (e.g. an agreement violation).
  public bool IsAgainst(AppealOrderSource source, Guid id) => OrderSourceTable == source && OrderSourceId == id;

  public void StartHearing()
  {
    if (AppealStatus != AppealStatus.Filed)
      throw new DomainException("Only a filed appeal can move to hearing.");

    AppealStatus = AppealStatus.UnderHearing;
  }

  /// The decision is final (s.32); it is a recorded event, so not in the future.
  public void Decide(DateOnly decisionDate, AppealOutcome outcome, string? details, DateOnly? today = null)
  {
    EnsureOpen();

    if (!Enum.IsDefined(outcome))
      throw new DomainException("Unknown decision outcome.");

    if (decisionDate < AppealDate)
      throw new DomainException("An appeal cannot be decided before it was filed.");

    if (today is { } day && decisionDate > day)
      throw new DomainException($"The decision date cannot be in the future ({decisionDate:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    DecisionDate = decisionDate;
    DecisionOutcome = outcome;
    DecisionDetails = Guard.Text(details, 4000, "Decision details");
    AppealStatus = AppealStatus.Decided;
  }

  public void Withdraw(string? remarks)
  {
    EnsureOpen();
    AppealStatus = AppealStatus.Withdrawn;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// Decided but not within the 120 days the Act allows.
  public bool IsOverdue(DateOnly today) => AppealStatus is AppealStatus.Filed or AppealStatus.UnderHearing && today > DecisionDueDate;

  private void EnsureOpen()
  {
    if (AppealStatus is AppealStatus.Decided or AppealStatus.Withdrawn)
      throw new DomainException($"Appeal {AppealNo.Value} is {Describe(AppealStatus)} and closed.");
  }

  private void EnsureOnlyAppealAgainstOrder(IReadOnlyCollection<PropertyAppeal> otherAppeals)
  {
    var earlier = otherAppeals
        .Where(a => a.Id != Id && a.PropertyId == PropertyId && a.AppealStatus != AppealStatus.Withdrawn && a.AgainstSameOrderAs(this))
        .OrderByDescending(a => a.IsOpen)
        .FirstOrDefault();

    if (earlier is null)
      return;

    if (earlier.IsOpen)
      throw new DomainException($"Appeal {earlier.AppealNo.Value} against order {earlier.AppealedOrderRef} is still {Describe(earlier.AppealStatus)}. Record its decision, or withdraw it, before filing another appeal against the same order.");

    throw new DomainException($"Appeal {earlier.AppealNo.Value} against order {earlier.AppealedOrderRef} was decided on {earlier.DecisionDate:yyyy-MM-dd}; the decision is final (Act s.32), so the order cannot be appealed again.");
  }

  /// Same record when both name one; otherwise the same order reference.
  private bool AgainstSameOrderAs(PropertyAppeal other) =>
      OrderSourceTable is not null && OrderSourceId is not null && other.OrderSourceTable is not null && other.OrderSourceId is not null
        ? OrderSourceTable == other.OrderSourceTable && OrderSourceId == other.OrderSourceId
        : string.Equals(AppealedOrderRef, other.AppealedOrderRef, StringComparison.OrdinalIgnoreCase);

  private static string Describe(AppealStatus status) => status switch
  {
    AppealStatus.Filed => "pending",
    AppealStatus.UnderHearing => "under hearing",
    AppealStatus.Decided => "decided",
    _ => "withdrawn"
  };

  private IReadOnlyList<string> Apply(Filing filing)
  {
    ArgumentNullException.ThrowIfNull(filing);
    ArgumentNullException.ThrowIfNull(filing.Appellant);
    filing.Appellant.EnsureActive();

    if (filing.OrderSourceTable is { } source && !Enum.IsDefined(source))
      throw new DomainException("Unknown order source.");

    if (filing.OrderSourceId is not null && filing.OrderSourceTable is null)
      throw new DomainException("order_source_id needs order_source_table.");

    Guard.DateOrder(filing.AppealedOrderDate, filing.OrderReceivedDate, "Order date", "Order received date");
    Guard.DateOrder(filing.AppealedOrderDate, filing.AppealDate, "Order date", "Appeal date");

    AppellantOwnerId = filing.Appellant.Id;
    AppealedOrderRef = Guard.RequiredText(filing.AppealedOrderRef, 100, "Appealed order ref");
    AppealedOrderDate = filing.AppealedOrderDate;
    OrderReceivedDate = filing.OrderReceivedDate;
    OrderSourceTable = filing.OrderSourceTable;
    OrderSourceId = filing.OrderSourceId;
    AppealDate = filing.AppealDate;
    AppellateAuthority = Guard.Text(filing.AppellateAuthority, 150, "Appellate authority") ?? DefaultAppellateAuthority;
    DelegatedOfficer = Guard.Text(filing.DelegatedOfficer, 150, "Delegated officer");
    DelegatedOfficerEmployeeId = DelegatedOfficer is null ? null : filing.DelegatedOfficerEmployeeId;
    Remarks = Guard.Text(filing.Remarks, 4000, "Remarks");

    // rule 6
    DecisionDueDate = AppealDate.AddDays(DecisionWindowDays);

    var warnings = new List<string>();
    var received = OrderReceivedDate ?? AppealedOrderDate;
    var daysAfterReceipt = AppealDate.DayNumber - received.DayNumber;

    if (daysAfterReceipt > FilingWindowDays)
      warnings.Add($"The appeal was filed {daysAfterReceipt} days after the order was {(OrderReceivedDate is null ? "passed" : "received")}; Act s.32(1) allows {FilingWindowDays} days.");

    return warnings;
  }
}
