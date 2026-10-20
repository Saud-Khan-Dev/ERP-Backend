/// taxation.demand: a year's demand on one tax file. It holds the year's charges (demand_line), the dues of earlier
/// years and other charges billed on it (arrears, by year and head) and its surcharges (penalty). It is computed as a
/// DRAFT, issued on approval, then paid against the property's constant reference oldest year first. When the next
/// year's demand is issued, whatever is unpaid moves onto it as arrears rows by year and head and this one is CARRIED.
public class Demand : Aggregate<DemandId>
{
  private readonly List<DemandLine> _lines = new();
  private readonly List<Arrears> _arrearsRows = new();
  private readonly List<Penalty> _penalties = new();

  public string DemandNo { get; private set; } = default!;
  public DemandRunId? RunId { get; private set; }
  public TaxFileId TaxFileId { get; private set; } = default!;
  public TaxFileBasisId BasisId { get; private set; } = default!;
  public int FiscalYear { get; private set; }
  public TaxFilePayerId PayerId { get; private set; } = default!;
  public string PayerName { get; private set; } = default!;
  public string? PayerCnic { get; private set; }
  public string? PayerMobile { get; private set; }
  public string? PayerAddress { get; private set; }
  public decimal CurrentAmount { get; private set; }
  public decimal ArrearsBf { get; private set; }
  public decimal PenaltyAmount { get; private set; }
  public decimal PaidAmount { get; private set; }
  public decimal Balance { get; private set; }
  public DemandStatus Status { get; private set; }
  public DemandStage? Stage { get; private set; }
  public DateOnly? IssuedOn { get; private set; }
  public DateOnly? DueDate { get; private set; }
  public DateOnly? PaidOn { get; private set; }
  public decimal? CarriedAmount { get; private set; }
  public DemandId? CarriedToDemandId { get; private set; }

  public IReadOnlyList<DemandLine> Lines => _lines.AsReadOnly();
  public IReadOnlyList<Arrears> ArrearsRows => _arrearsRows.AsReadOnly();
  public IReadOnlyList<Penalty> Penalties => _penalties.AsReadOnly();

  private Demand() { }

  /// Issued and still owing something this year (not draft, carried, paid or cancelled).
  public bool IsOpen => (Status is DemandStatus.Issued or DemandStatus.PartPaid or DemandStatus.Defaulted) && Balance > 0;

  public bool IsIssued => Status is not (DemandStatus.Draft or DemandStatus.Cancelled);

  public decimal PenaltySum(PenaltyKind? kind = null) => _penalties.Where(p => kind is null || p.Kind == kind).Sum(p => p.Amount);

  /// A computed demand awaiting approval. The arrears it will carry are only shown (arrearsPreview); they are moved onto
  /// it when it is issued.
  public static Demand Draft(DemandId id, string demandNo, DemandRunId? runId, TaxFile file, TaxFileBasis basis, TaxFilePayer payer, int fiscalYear,
      IReadOnlyList<ComputedLine> lines, decimal arrearsPreview)
  {
    var demand = new Demand
    {
      Id = id,
      DemandNo = Guard.RequiredText(demandNo, 40, "Demand no."),
      RunId = runId,
      TaxFileId = file.Id,
      BasisId = basis.Id,
      FiscalYear = global::FiscalYear.Valid(fiscalYear),
      Status = DemandStatus.Draft
    };
    demand.Address(payer);
    foreach (var line in lines)
      demand._lines.Add(DemandLine.From(DemandLineId.New(), id, line));
    demand.Recalculate();
    demand.ArrearsBf = Rupees.Round(arrearsPreview);
    demand.Balance = demand.CurrentAmount + demand.ArrearsBf;
    return demand;
  }

  /// Replaces a draft's computation with a fresh one.
  public void Recompute(DemandRunId? runId, TaxFileBasis basis, TaxFilePayer payer, IReadOnlyList<ComputedLine> lines, decimal arrearsPreview)
  {
    if (Status != DemandStatus.Draft)
      throw new DomainException($"{DemandNo} is already issued; its amounts are fixed.");
    RunId = runId;
    BasisId = basis.Id;
    Address(payer);
    _lines.Clear();
    foreach (var line in lines)
      _lines.Add(DemandLine.From(DemandLineId.New(), Id, line));
    Recalculate();
    ArrearsBf = Rupees.Round(arrearsPreview);
    Balance = CurrentAmount + ArrearsBf;
  }

  public void Address(TaxFilePayer payer)
  {
    PayerId = payer.Id;
    PayerName = payer.Name;
    PayerCnic = payer.CnicNtn;
    PayerMobile = payer.Mobile;
    PayerAddress = payer.PostalAddress;
  }

  /// Issues a draft: the earlier years' unpaid balances (already moved onto it) and the opening arrears of the old
  /// register become part of it, and it is due by the year's due date.
  public void Issue(DateOnly issuedOn, DateOnly dueDate, IEnumerable<Arrears> billedArrears)
  {
    if (Status != DemandStatus.Draft)
      throw new DomainException($"{DemandNo} is already issued.");
    foreach (var row in billedArrears)
      AttachArrears(row);
    Status = DemandStatus.Issued;
    IssuedOn = issuedOn;
    DueDate = dueDate;
    Recalculate();
    if (Balance <= 0)
    {
      Status = DemandStatus.Paid;
      PaidOn = issuedOn;
    }
  }

  public void Cancel()
  {
    if (Status != DemandStatus.Draft)
      throw new DomainException($"{DemandNo} is issued; an issued demand is corrected, not cancelled.");
    Status = DemandStatus.Cancelled;
  }

  internal void AttachArrears(Arrears row)
  {
    row.BillOn(Id);
    if (!_arrearsRows.Contains(row))
      _arrearsRows.Add(row);
  }

  // ---- what is owed, item by item ----

  /// Every chargeable item in the order a payment settles them: earlier years' dues (oldest first), this year's heads,
  /// then surcharges. paidByItem holds what payments already settled per item (from payment_allocation).
  public IReadOnlyList<DemandItem> Items(IReadOnlyDictionary<Guid, decimal> paidByItem, TaxHeadId surchargeHeadId)
  {
    var items = new List<DemandItem>();
    foreach (var row in _arrearsRows.OrderBy(a => a.FiscalYear ?? 0).ThenBy(a => a.CreatedAt ?? DateTime.MinValue))
      items.Add(new DemandItem(DemandItemKind.Arrears, row.Id.Value, row.HeadId, row.FiscalYear, row.OriginalAmount + row.PenaltyAmount, row.OriginalAmount + row.PenaltyAmount - row.Outstanding, true));
    foreach (var line in _lines)
      items.Add(new DemandItem(DemandItemKind.Line, line.Id.Value, line.HeadId, FiscalYear, line.Amount, paidByItem.GetValueOrDefault(line.Id.Value), false));
    foreach (var penalty in _penalties.OrderBy(p => p.AppliedOn))
      items.Add(new DemandItem(DemandItemKind.Penalty, penalty.Id.Value, surchargeHeadId, FiscalYear, penalty.Amount, paidByItem.GetValueOrDefault(penalty.Id.Value), false));
    return items;
  }

  /// Settles up to `amount` of this demand, item by item. Returns what went where.
  public IReadOnlyList<ItemSettlement> Settle(decimal amount, IReadOnlyDictionary<Guid, decimal> paidByItem, TaxHeadId surchargeHeadId, DateOnly paidOn)
  {
    if (!IsOpen)
      throw new DomainException($"{DemandNo} has nothing to pay.");
    if (amount <= 0)
      throw new DomainException("The amount must be positive.");

    var left = Math.Min(amount, Balance);
    var settled = new List<ItemSettlement>();
    foreach (var item in Items(paidByItem, surchargeHeadId))
    {
      if (left <= 0)
        break;
      var take = Math.Min(left, item.Outstanding);
      if (take <= 0)
        continue;
      settled.Add(new ItemSettlement(item.Kind, item.ItemId, item.HeadId, take));
      if (item.Kind == DemandItemKind.Arrears)
        _arrearsRows.First(a => a.Id.Value == item.ItemId).Settle(take);
      left -= take;
    }

    PaidAmount += settled.Sum(s => s.Amount);
    Recalculate();
    if (Balance <= 0)
    {
      Status = DemandStatus.Paid;
      PaidOn = paidOn;
    }
    else if (Status != DemandStatus.Defaulted)
    {
      Status = DemandStatus.PartPaid;
    }
    return settled;
  }

  /// Moves whatever is unpaid onto the next year's demand as arrears rows by year and head (this year's heads at this
  /// year, earlier rows at their own year, surcharges under SURCHARGE), and marks this demand CARRIED.
  public IReadOnlyList<Arrears> CarryInto(Demand next, IReadOnlyDictionary<Guid, decimal> paidByItem, TaxHeadId surchargeHeadId, Guid? actor, DateTime now)
  {
    if (next.TaxFileId != TaxFileId || next.FiscalYear <= FiscalYear)
      throw new DomainException("A balance is carried only into a later year's demand of the same file.");
    if (Status is DemandStatus.Draft or DemandStatus.Cancelled or DemandStatus.Carried or DemandStatus.Paid || Balance <= 0)
      return [];

    var rows = Items(paidByItem, surchargeHeadId)
      .Where(i => i.Outstanding > 0)
      .GroupBy(i => (i.FiscalYear, i.HeadId))
      .Select(g => Arrears.Carried(ArrearsId.New(), TaxFileId, g.Key.HeadId, g.Key.FiscalYear, g.Sum(i => i.Outstanding), Id, actor, now))
      .ToList();

    // everything carried is settled here (the rows on the next demand hold it now)
    foreach (var row in _arrearsRows)
      row.Settle(row.Outstanding);

    CarriedAmount = Balance;
    CarriedToDemandId = next.Id;
    Status = DemandStatus.Carried;
    Balance = 0;
    foreach (var row in rows)
      next.AttachArrears(row);
    next.Recalculate();
    return rows;
  }

  // ---- penalties ----

  public Penalty AddPenalty(PenaltyRule rule, PenaltyRunId? runId, decimal baseAmount, decimal amount, decimal? pct, DateOnly appliedOn)
  {
    if (!IsOpen)
      throw new DomainException($"{DemandNo} has nothing outstanding; no surcharge applies.");
    if (amount <= 0)
      throw new DomainException("A surcharge must be more than zero.");
    var penalty = Penalty.Create(PenaltyId.New(), Id, rule, runId, baseAmount, amount, pct, appliedOn);
    _penalties.Add(penalty);
    Recalculate();
    return penalty;
  }

  /// The automatic year-end surcharge: once 30 June of the demand's year is over, the rule's rate on what is still
  /// outstanding; the demand becomes DEFAULTED. Applied once.
  public Penalty? ApplyYearEnd(PenaltyRule rule, DateOnly today)
  {
    if (rule.Kind != PenaltyKind.YearEnd)
      throw new DomainException($"{rule.Name} is not the year-end surcharge.");
    if (Status is not (DemandStatus.Issued or DemandStatus.PartPaid) || today <= global::FiscalYear.End(FiscalYear) || PenaltySum(PenaltyKind.YearEnd) > 0 || Balance <= 0)
      return null;

    var baseAmount = rule.StacksWithYearEnd == false ? Math.Max(0, Balance - PenaltySum(PenaltyKind.IntraYear)) : Balance;
    var amount = rule.AmountOn(baseAmount);
    var pct = rule.RateType == PenaltyRateType.Percent ? rule.Rate : (decimal?)null;
    var penalty = amount > 0 ? AddPenalty(rule, null, baseAmount, amount, pct, global::FiscalYear.End(FiscalYear)) : null;
    Status = DemandStatus.Defaulted;
    Stage = DemandStage.Penalty;
    return penalty;
  }

  // ---- changes made with a notice ----

  public DemandLine Line(TaxHeadId headId) =>
      _lines.FirstOrDefault(l => l.HeadId == headId) ?? throw new DomainException("The demand has no line for that head.");

  /// Earlier years' dues or other charges entered when a notice is created.
  public Arrears AddManualArrears(TaxHead head, int? fiscalYear, decimal amount, string? description, string reason, Guid actor, DateTime now)
  {
    if (Status is DemandStatus.Carried or DemandStatus.Cancelled)
      throw new DomainException($"{DemandNo} is {EnumText.Words(Status)}; charges are added to the demand that holds the balance.");
    if (fiscalYear > FiscalYear)
      throw new DomainException($"A row for FY {(global::FiscalYear.Label(fiscalYear.Value))} is after the demand's year.");
    if (head.Code == HeadCodes.Misc && string.IsNullOrWhiteSpace(description))
      throw new DomainException("Describe each miscellaneous charge (what it is for).");
    var row = Arrears.Manual(ArrearsId.New(), TaxFileId, head.Id, fiscalYear, amount, description, reason, actor, now);
    AttachArrears(row);
    AfterChange(DateOnly.FromDateTime(now));
    return row;
  }

  /// After amounts changed on an issued demand: a paid demand that now owes again is reopened, one now fully covered is paid.
  public void AfterChange(DateOnly today)
  {
    Recalculate();
    if (Status == DemandStatus.Paid && Balance > 0)
    {
      Status = PaidAmount > 0 ? DemandStatus.PartPaid : DemandStatus.Issued;
      PaidOn = null;
    }
    else if (Balance <= 0 && (Status is DemandStatus.Issued or DemandStatus.PartPaid))
    {
      Status = DemandStatus.Paid;
      PaidOn = today;
    }
  }

  /// What was paid beyond the charges after a correction (to be refunded or credited).
  public decimal Excess => Math.Max(0, PaidAmount - (CurrentAmount + ArrearsBf + PenaltyAmount));

  public void MarkStage(NoticeStage stage)
  {
    var next = stage switch
    {
      NoticeStage.First => DemandStage.First,
      NoticeStage.Second => DemandStage.Second,
      NoticeStage.Final => DemandStage.Final,
      NoticeStage.ShowCause => DemandStage.ShowCause,
      _ => DemandStage.Penalty
    };
    if (Stage is null || next >= Stage)
      Stage = next;
  }

  internal void Recalculate()
  {
    CurrentAmount = _lines.Sum(l => l.Amount);
    ArrearsBf = _arrearsRows.Sum(a => a.OriginalAmount + a.PenaltyAmount);
    PenaltyAmount = PenaltySum();
    Balance = Status == DemandStatus.Carried ? 0 : Math.Max(0, CurrentAmount + ArrearsBf + PenaltyAmount - PaidAmount);
  }
}

public enum DemandItemKind
{
  Arrears,
  Line,
  Penalty
}

/// One chargeable item of a demand and what is still owed on it.
public sealed record DemandItem(DemandItemKind Kind, Guid ItemId, TaxHeadId HeadId, int? FiscalYear, decimal Amount, decimal Paid, bool IsArrears)
{
  public decimal Outstanding => Math.Max(0, Amount - Paid);
}

public sealed record ItemSettlement(DemandItemKind Kind, Guid ItemId, TaxHeadId HeadId, decimal Amount);

/// taxation.demand_line: one head of the year's demand with how it was worked out.
public class DemandLine : Entity<DemandLineId>
{
  public DemandId DemandId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public decimal? BasisQty { get; private set; }
  public string? BasisUnit { get; private set; }
  public LtfFactorId? LtfFactorId { get; private set; }
  public RateBandId? RateBandId { get; private set; }
  public Guid? ScheduleLineId { get; private set; }
  public decimal BaseAmount { get; private set; }
  public decimal IncreaseAmount { get; private set; }
  public decimal UpliftAmount { get; private set; }
  public TaxFileOverrideId? OverrideId { get; private set; }
  public LineMode LineMode { get; private set; }
  public SpecialPolicyId? PolicyId { get; private set; }
  public decimal? NormalAmount { get; private set; }
  public decimal OverrideAmount { get; private set; }
  public decimal Amount { get; private set; }
  public string? Explanation { get; private set; }

  private DemandLine() { }

  internal static DemandLine From(DemandLineId id, DemandId demandId, ComputedLine c) => new()
  {
    Id = id,
    DemandId = demandId,
    HeadId = c.HeadId,
    BasisQty = c.BasisQty,
    BasisUnit = c.BasisUnit is null ? null : c.BasisUnit.Length <= 10 ? c.BasisUnit : c.BasisUnit[..10],
    LtfFactorId = c.LtfFactorId,
    RateBandId = c.RateBandId,
    BaseAmount = c.BaseAmount,
    IncreaseAmount = c.IncreaseAmount,
    UpliftAmount = c.UpliftAmount,
    OverrideId = c.OverrideId,
    LineMode = c.Mode,
    PolicyId = c.PolicyId,
    NormalAmount = c.NormalAmount,
    OverrideAmount = c.OverrideAmount,
    Amount = c.Amount,
    Explanation = Clip(c.Explanation)
  };

  /// Switches the head to a special policy (Special on a notice).
  public void ToPolicy(SpecialPolicyId policyId, decimal amount, string explanation)
  {
    BaseAmount = amount;
    IncreaseAmount = 0;
    UpliftAmount = 0;
    OverrideAmount = 0;
    Amount = amount;
    LineMode = LineMode.Policy;
    PolicyId = policyId;
    OverrideId = null;
    Explanation = Clip(explanation);
  }

  /// Switches the head back to the normal formula (Normal on a notice).
  public void ToNormal(decimal normalBase, decimal normalIncrease, decimal normalUplift, string explanation)
  {
    BaseAmount = normalBase;
    IncreaseAmount = normalIncrease;
    UpliftAmount = normalUplift;
    OverrideAmount = 0;
    Amount = normalBase + normalIncrease + normalUplift;
    LineMode = LineMode.Normal;
    PolicyId = null;
    OverrideId = null;
    Explanation = Clip(explanation);
  }

  /// An amount typed in when a notice creates the demand; the difference is kept as the override amount.
  public void SetManual(decimal amount)
  {
    var value = Rupees.Round(Guard.NotNegative(amount, "Amount"));
    OverrideAmount += value - Amount;
    Amount = value;
    LineMode = LineMode.Manual;
  }

  private static string? Clip(string? text) => text is null ? null : text.Length <= 500 ? text : text[..497] + "...";
}

/// taxation.arrears: dues by year and head billed on a demand: an earlier year's unpaid balance (DEMAND), the old
/// register's opening arrears (IMPORT, not yet billed until the first demand), or charges entered on a notice (MANUAL).
public class Arrears : Entity<ArrearsId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public int? FiscalYear { get; private set; }
  public decimal OriginalAmount { get; private set; }
  public decimal PenaltyAmount { get; private set; }
  public decimal Outstanding { get; private set; }
  public ArrearsSource Source { get; private set; }
  public DemandId? SourceDemandId { get; private set; }
  public Guid? ImportRowId { get; private set; }
  public DemandId? AddedToDemandId { get; private set; }
  public string? Description { get; private set; }
  public Guid? EnteredBy { get; private set; }
  public DateTime? EnteredAt { get; private set; }
  public string? Reason { get; private set; }

  private Arrears() { }

  internal static Arrears Carried(ArrearsId id, TaxFileId fileId, TaxHeadId headId, int? fiscalYear, decimal amount, DemandId sourceDemandId, Guid? actor, DateTime now) => new()
  {
    Id = id,
    TaxFileId = fileId,
    HeadId = headId,
    FiscalYear = fiscalYear,
    OriginalAmount = Rupees.Round(amount),
    Outstanding = Rupees.Round(amount),
    Source = ArrearsSource.Demand,
    SourceDemandId = sourceDemandId,
    EnteredBy = actor,
    EnteredAt = now
  };

  /// Opening arrears from the old register, by head (and year when known). Billed on the file's first demand.
  public static Arrears Imported(ArrearsId id, TaxFileId fileId, TaxHead head, int? fiscalYear, decimal amount, string? description, Guid? importRowId, Guid actor, DateTime now) =>
      head.Code == HeadCodes.Misc && string.IsNullOrWhiteSpace(description)
        ? throw new DomainException("Describe each miscellaneous charge (what it is for).")
        : new()
  {
    Id = id,
    TaxFileId = fileId,
    HeadId = head.Id,
    FiscalYear = fiscalYear is { } fy ? global::FiscalYear.Valid(fy) : null,
    OriginalAmount = Rupees.Round(Guard.Positive(amount, "Amount")),
    Outstanding = Rupees.Round(amount),
    Source = ArrearsSource.Import,
    ImportRowId = importRowId,
    Description = Guard.Text(description, 300, "Description"),
    EnteredBy = actor,
    EnteredAt = now,
    Reason = "Opening arrears of the old register"
  };

  internal static Arrears Manual(ArrearsId id, TaxFileId fileId, TaxHeadId headId, int? fiscalYear, decimal amount, string? description, string reason, Guid actor, DateTime now) => new()
  {
    Id = id,
    TaxFileId = fileId,
    HeadId = headId,
    FiscalYear = fiscalYear is { } fy ? global::FiscalYear.Valid(fy) : null,
    OriginalAmount = Rupees.Round(Guard.Positive(amount, "Amount")),
    Outstanding = Rupees.Round(amount),
    Source = ArrearsSource.Manual,
    Description = Guard.Text(description, 300, "Description"),
    EnteredBy = actor,
    EnteredAt = now,
    Reason = Guard.RequiredText(reason, 300, "Reason")
  };

  internal void BillOn(DemandId demandId)
  {
    if (AddedToDemandId is not null && AddedToDemandId != demandId)
      throw new DomainException("This arrears row is already billed on another demand.");
    AddedToDemandId = demandId;
  }

  internal void Settle(decimal amount) => Outstanding = Math.Max(0, Outstanding - amount);
}

/// taxation.penalty: a surcharge on a demand (in-year run or the automatic year-end percentage).
public class Penalty : Entity<PenaltyId>
{
  public DemandId DemandId { get; private set; } = default!;
  public PenaltyRuleId? RuleId { get; private set; }
  public PenaltyRunId? RunId { get; private set; }
  public PenaltyKind Kind { get; private set; }
  public decimal BaseAmount { get; private set; }
  public decimal? Pct { get; private set; }
  public decimal Amount { get; private set; }
  public DateOnly AppliedOn { get; private set; }
  public string? RuleRef { get; private set; }
  public Guid? ViolationId { get; private set; }

  private Penalty() { }

  internal static Penalty Create(PenaltyId id, DemandId demandId, PenaltyRule rule, PenaltyRunId? runId, decimal baseAmount, decimal amount, decimal? pct, DateOnly appliedOn) => new()
  {
    Id = id,
    DemandId = demandId,
    RuleId = rule.Id,
    RunId = runId,
    Kind = rule.Kind,
    BaseAmount = Rupees.Round(baseAmount),
    Pct = pct is { } p ? Math.Round(p, 2) : null,
    Amount = Rupees.Round(amount),
    AppliedOn = appliedOn,
    RuleRef = rule.Code
  };
}
