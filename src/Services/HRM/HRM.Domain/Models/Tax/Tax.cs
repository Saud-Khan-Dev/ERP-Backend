public sealed record TaxSlabInput(int SlabOrder, decimal MinIncome, decimal? MaxIncome, decimal FixedAmount, decimal RatePercentage);

/// An income-tax year (e.g. 2025-26, 1 July - 30 June) with its slabs. Tax years never overlap.
public class TaxYear : Aggregate<TaxYearId>
{
  private readonly List<TaxSlab> _slabs = new();

  public string YearLabel { get; private set; } = default!;
  public DateOnly StartDate { get; private set; }
  public DateOnly EndDate { get; private set; }
  public RecordStatus Status { get; private set; }

  public IReadOnlyList<TaxSlab> Slabs => _slabs.OrderBy(s => s.SlabOrder).ToList().AsReadOnly();

  public DateRange Range => new(StartDate, EndDate);

  public static TaxYear Create(TaxYearId id, string yearLabel, DateOnly startDate, DateOnly endDate, IReadOnlyCollection<TaxSlabInput> slabs)
  {
    var year = new TaxYear { Id = id, Status = RecordStatus.Active };
    year.Update(yearLabel, startDate, endDate);
    year.ReplaceSlabs(slabs);
    return year;
  }

  public void Update(string yearLabel, DateOnly startDate, DateOnly endDate)
  {
    if (endDate <= startDate)
      throw new DomainException("A tax year must end after it starts.");

    YearLabel = Guard.RequiredText(yearLabel, 20, "Tax year");
    StartDate = startDate;
    EndDate = endDate;
  }

  public void SetStatus(RecordStatus status) => Status = status;

  /// Slabs are [min, max): an income equal to a slab's maximum falls in the next slab. They may not overlap.
  public void ReplaceSlabs(IReadOnlyCollection<TaxSlabInput> slabs)
  {
    ArgumentNullException.ThrowIfNull(slabs);

    if (slabs.Select(s => s.SlabOrder).Distinct().Count() != slabs.Count)
      throw new DomainException("Each slab order may appear only once.");

    var ordered = slabs.OrderBy(s => s.MinIncome).ToList();
    for (var i = 0; i < ordered.Count; i++)
    {
      var slab = ordered[i];
      Guard.NotNegative(slab.MinIncome, "Slab minimum");
      if (slab.MaxIncome is { } max && max <= slab.MinIncome)
        throw new DomainException($"Slab {slab.SlabOrder}: the maximum must be above the minimum.");
      if (slab.MaxIncome is null && i != ordered.Count - 1)
        throw new DomainException($"Slab {slab.SlabOrder} is open-ended, so it must be the highest slab.");
      if (i > 0 && ordered[i - 1].MaxIncome is { } previousMax && slab.MinIncome < previousMax)
        throw new DomainException($"Slab {slab.SlabOrder} overlaps the slab below it.");
    }

    _slabs.Clear();
    _slabs.AddRange(slabs.Select(s => TaxSlab.Create(Id, s)));
  }

  /// Annual tax on a taxable income: the slab's fixed amount plus its rate on the income above the slab's minimum.
  public decimal AnnualTax(decimal taxableIncome)
  {
    if (taxableIncome <= 0)
      return 0;

    var slab = _slabs.FirstOrDefault(s => taxableIncome >= s.MinIncome && (s.MaxIncome is null || taxableIncome < s.MaxIncome));
    if (slab is null)
      return 0;

    return decimal.Round(slab.FixedAmount + (taxableIncome - slab.MinIncome) * slab.RatePercentage / 100m, 2, MidpointRounding.AwayFromZero);
  }

  /// Payroll months of this tax year from the given month to the end of the year (inclusive).
  public int MonthsRemainingFrom(int year, int month)
  {
    var from = new DateOnly(year, month, 1);
    if (from > EndDate)
      return 0;
    if (from < new DateOnly(StartDate.Year, StartDate.Month, 1))
      from = new DateOnly(StartDate.Year, StartDate.Month, 1);
    return (EndDate.Year - from.Year) * 12 + EndDate.Month - from.Month + 1;
  }
}

public class TaxSlab : Entity<TaxSlabId>
{
  public TaxYearId TaxYearId { get; private set; } = default!;
  public int SlabOrder { get; private set; }
  public decimal MinIncome { get; private set; }
  public decimal? MaxIncome { get; private set; }
  public decimal FixedAmount { get; private set; }
  public decimal RatePercentage { get; private set; }

  internal static TaxSlab Create(TaxYearId taxYearId, TaxSlabInput input) => new()
  {
    Id = TaxSlabId.New(),
    TaxYearId = taxYearId,
    SlabOrder = input.SlabOrder,
    MinIncome = Guard.Money(input.MinIncome, "Slab minimum"),
    MaxIncome = Guard.Money(input.MaxIncome, "Slab maximum"),
    FixedAmount = Guard.Money(input.FixedAmount, "Fixed tax"),
    RatePercentage = Guard.Between(input.RatePercentage, 0, 100, "Tax rate")
  };
}

/// An amount taken off an employee's taxable income for a tax year (e.g. Zakat, donations), one per exemption type.
public class EmployeeTaxExemption : Aggregate<TaxExemptionId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public TaxYearId TaxYearId { get; private set; } = default!;
  public string ExemptionType { get; private set; } = default!;
  public decimal Amount { get; private set; }

  public static EmployeeTaxExemption Create(TaxExemptionId id, EmployeeId employeeId, TaxYear taxYear, string exemptionType, decimal amount)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(taxYear);

    var exemption = new EmployeeTaxExemption { Id = id, EmployeeId = employeeId, TaxYearId = taxYear.Id };
    exemption.Update(exemptionType, amount);
    return exemption;
  }

  public void Update(string exemptionType, decimal amount)
  {
    ExemptionType = Guard.RequiredText(exemptionType, 100, "Exemption type");
    Amount = Guard.Money(amount, "Exemption amount");
  }
}

/// Taxable income and tax withheld by one payroll transaction, for the year-to-date tax of the next months.
public class EmployeeTaxLedgerEntry : Aggregate<TaxLedgerEntryId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public TaxYearId TaxYearId { get; private set; } = default!;
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public decimal TaxableIncome { get; private set; }
  public decimal TaxWithheld { get; private set; }

  public static EmployeeTaxLedgerEntry Create(EmployeeId employeeId, TaxYearId taxYearId, PayrollTransactionId payrollTransactionId, decimal taxableIncome, decimal taxWithheld) => new()
  {
    Id = TaxLedgerEntryId.New(),
    EmployeeId = employeeId,
    TaxYearId = taxYearId,
    PayrollTransactionId = payrollTransactionId,
    TaxableIncome = Guard.Money(taxableIncome, "Taxable income"),
    TaxWithheld = Guard.Money(taxWithheld, "Tax withheld")
  };

  public void Replace(decimal taxableIncome, decimal taxWithheld)
  {
    TaxableIncome = Guard.Money(taxableIncome, "Taxable income");
    TaxWithheld = Guard.Money(taxWithheld, "Tax withheld");
  }
}
