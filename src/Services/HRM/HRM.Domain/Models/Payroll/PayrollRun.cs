/// A payroll month: open (runs can be made), closed (nothing new, can reopen), locked (final).
public class PayrollPeriod : Aggregate<PayrollPeriodId>
{
  public int Year { get; private set; }
  public int Month { get; private set; }
  public DateOnly StartDate { get; private set; }
  public DateOnly EndDate { get; private set; }
  public PayrollPeriodStatus Status { get; private set; }

  public DateRange Range => new(StartDate, EndDate);

  public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;

  public string Label => $"{new DateOnly(Year, Month, 1):MMMM yyyy}";

  /// The calendar month by default.
  public static PayrollPeriod Create(PayrollPeriodId id, int year, int month, DateOnly? startDate, DateOnly? endDate)
  {
    Guard.Between(year, 2000, 2100, "Year");
    Guard.Between(month, 1, 12, "Month");

    var start = startDate ?? new DateOnly(year, month, 1);
    var end = endDate ?? new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    Guard.DateOrder(start, end, "Start date", "End date");

    if (end.DayNumber - start.DayNumber + 1 > 31)
      throw new DomainException("A payroll period cannot be longer than 31 days.");

    return new PayrollPeriod { Id = id, Year = year, Month = month, StartDate = start, EndDate = end, Status = PayrollPeriodStatus.Open };
  }

  /// No new run in this month. `unfinishedRuns` = runs still before finalization (they must be finished or discarded).
  public void Close(int unfinishedRuns)
  {
    if (Status != PayrollPeriodStatus.Open)
      throw new DomainException($"{Label} is already {EnumText.Words(Status)}.");
    if (unfinishedRuns > 0)
      throw new DomainException($"{Label} has {unfinishedRuns} run(s) not finalized yet. Finalize or delete them first.");

    Status = PayrollPeriodStatus.Closed;
  }

  public void Reopen()
  {
    if (Status != PayrollPeriodStatus.Closed)
      throw new DomainException(Status == PayrollPeriodStatus.Locked ? $"{Label} is locked for good." : $"{Label} is already open.");

    Status = PayrollPeriodStatus.Open;
  }

  /// Final: the month can never be reopened.
  public void Lock()
  {
    if (Status != PayrollPeriodStatus.Closed)
      throw new DomainException($"Close {Label} before locking it.");

    Status = PayrollPeriodStatus.Locked;
  }

  public void EnsureOpen()
  {
    if (Status != PayrollPeriodStatus.Open)
      throw new DomainException($"{Label} is {EnumText.Words(Status)}; no payroll can be run in it.");
  }
}

/// One payroll run of a period. A period has at most one live regular run; supplementary, arrears, bonus and final
/// settlement runs may repeat. Workflow: draft -> calculated -> reviewed -> approved -> finalized -> paid, each step
/// back one stage allowed before finalization; a finalized or paid run can only be reversed. From finalization on the
/// run's figures are locked (database triggers refuse any change).
public class PayrollRun : Aggregate<PayrollRunId>
{
  public PayrollPeriodId PayrollPeriodId { get; private set; } = default!;
  public PayrollRunType RunType { get; private set; }
  public string? RunLabel { get; private set; }
  public PayrollRunStatus Status { get; private set; }
  /// The reversed run this one replaces.
  public PayrollRunId? ReversesRunId { get; private set; }
  public Guid? PreparedBy { get; private set; }
  public Guid? ReviewedBy { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovalDate { get; private set; }
  public DateTime? FinalizationDate { get; private set; }
  public DateTime? PaymentDate { get; private set; }

  public bool IsLive => Status != PayrollRunStatus.Reversed;

  public bool IsLocked => Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid or PayrollRunStatus.Reversed;

  /// Runs whose employees are worked out by the engine (pay, allowances, deductions); the others are made of
  /// adjustments entered per employee.
  public bool IsComputed => RunType is PayrollRunType.Regular or PayrollRunType.Supplementary;

  public static PayrollRun Create(PayrollRunId id, PayrollPeriod period, PayrollRunType runType, string? runLabel, PayrollRun? replaces, Guid? preparedBy)
  {
    ArgumentNullException.ThrowIfNull(period);
    period.EnsureOpen();

    if (replaces is not null)
    {
      if (replaces.Status != PayrollRunStatus.Reversed)
        throw new DomainException("A run can only replace a reversed run.");
      if (replaces.PayrollPeriodId != period.Id)
        throw new DomainException("A replacement run must be in the same period as the run it replaces.");
    }

    return new PayrollRun
    {
      Id = id,
      PayrollPeriodId = period.Id,
      RunType = runType,
      RunLabel = Guard.Text(runLabel, 150, "Run label"),
      Status = PayrollRunStatus.Draft,
      ReversesRunId = replaces?.Id,
      PreparedBy = preparedBy
    };
  }

  public void Rename(string? runLabel)
  {
    EnsureEditable();
    RunLabel = Guard.Text(runLabel, 150, "Run label");
  }

  /// The figures were (re)worked out. Allowed while the run is a draft or calculated.
  public void MarkCalculated(Guid? preparedBy)
  {
    EnsureEditable();
    Status = PayrollRunStatus.Calculated;
    PreparedBy = preparedBy ?? PreparedBy;
  }

  /// Discards the calculation (the caller deletes the transactions).
  public void ResetToDraft() => Move(PayrollRunStatus.Calculated, PayrollRunStatus.Draft);

  public void Review(Guid? reviewedBy)
  {
    Move(PayrollRunStatus.Calculated, PayrollRunStatus.Reviewed);
    ReviewedBy = Guard.Actor(reviewedBy, "review a payroll run");
  }

  public void SendBackToCalculated()
  {
    Move(PayrollRunStatus.Reviewed, PayrollRunStatus.Calculated);
    ReviewedBy = null;
  }

  /// `requireSeparateApprover`: the person who prepared the run may not approve it.
  public void Approve(Guid? approvedBy, DateTime approvedAt, bool requireSeparateApprover)
  {
    var approver = Guard.Actor(approvedBy, "approve a payroll run");
    if (requireSeparateApprover && approver == PreparedBy)
      throw new DomainException("The person who prepared this payroll cannot also approve it.");

    Move(PayrollRunStatus.Reviewed, PayrollRunStatus.Approved);
    ApprovedBy = approver;
    ApprovalDate = approvedAt;
  }

  public void SendBackToReview()
  {
    Move(PayrollRunStatus.Approved, PayrollRunStatus.Reviewed);
    ApprovedBy = null;
    ApprovalDate = null;
  }

  /// Posts the run: from now on its figures are locked. Loan recoveries and GP Fund rows are posted with it.
  public void FinalizeRun(DateTime finalizedAt)
  {
    Move(PayrollRunStatus.Approved, PayrollRunStatus.Finalized);
    FinalizationDate = finalizedAt;
  }

  public void MarkPaid(DateTime paidAt)
  {
    Move(PayrollRunStatus.Finalized, PayrollRunStatus.Paid);
    PaymentDate = paidAt;
  }

  /// Undoes a finalized or paid run (the caller undoes its loan recoveries and GP Fund rows).
  public void Reverse()
  {
    if (Status is not (PayrollRunStatus.Finalized or PayrollRunStatus.Paid))
      throw new DomainException($"Only a finalized or paid run can be reversed; this one is {EnumText.Words(Status)}. A run before finalization is changed or deleted instead.");

    Status = PayrollRunStatus.Reversed;
  }

  public void EnsureEditable()
  {
    if (Status is not (PayrollRunStatus.Draft or PayrollRunStatus.Calculated))
      throw new DomainException($"The run is {EnumText.Words(Status)}; its figures can only change while it is a draft or calculated.");
  }

  private void Move(PayrollRunStatus from, PayrollRunStatus to)
  {
    if (Status != from)
      throw new DomainException($"The run is {EnumText.Words(Status)}; it must be {EnumText.Words(from)} to become {EnumText.Words(to)}.");

    Status = to;
  }
}
