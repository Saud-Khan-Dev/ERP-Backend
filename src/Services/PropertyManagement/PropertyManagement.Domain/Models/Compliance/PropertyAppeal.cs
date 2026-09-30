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
  public string? DelegatedOfficer { get; private set; }
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
      string? Remarks);

  public static PropertyAppeal File(AppealId id, Property property, BusinessCode appealNo, Filing filing, out IReadOnlyList<string> warnings)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(appealNo);
    property.EnsureActive();

    var appeal = new PropertyAppeal { Id = id, PropertyId = property.Id, AppealNo = appealNo, AppealStatus = AppealStatus.Filed };
    warnings = appeal.Apply(filing);
    return appeal;
  }

  public IReadOnlyList<string> Update(Filing filing)
  {
    if (AppealStatus != AppealStatus.Filed)
      throw new DomainException($"Appeal {AppealNo.Value} is {AppealStatus} and can no longer be edited.");

    return Apply(filing);
  }

  public void StartHearing()
  {
    if (AppealStatus != AppealStatus.Filed)
      throw new DomainException("Only a filed appeal can move to hearing.");

    AppealStatus = AppealStatus.UnderHearing;
  }

  /// The decision is final (s.32).
  public void Decide(DateOnly decisionDate, AppealOutcome outcome, string? details)
  {
    EnsureOpen();

    if (!Enum.IsDefined(outcome))
      throw new DomainException("Unknown decision outcome.");

    if (decisionDate < AppealDate)
      throw new DomainException("An appeal cannot be decided before it was filed.");

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
      throw new DomainException($"Appeal {AppealNo.Value} is {AppealStatus} and closed.");
  }

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
