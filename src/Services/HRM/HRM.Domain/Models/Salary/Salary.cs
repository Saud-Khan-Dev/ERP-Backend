/// A pay-slip line kind (Basic Pay, GDA Authority Allowance, BF, GP Fund, Income Tax ...).
public class SalaryComponent : Aggregate<SalaryComponentId>
{
  public string ComponentCode { get; private set; } = default!;
  public string ComponentName { get; private set; } = default!;
  public ComponentType ComponentType { get; private set; }
  public bool IsTaxable { get; private set; }
  public bool IsActive { get; private set; }

  public bool IsSystem => SystemComponents.IsSystem(ComponentCode);

  public static SalaryComponent Create(SalaryComponentId id, string code, string name, ComponentType type, bool isTaxable)
  {
    var component = new SalaryComponent { Id = id, ComponentCode = Guard.Code(code, 30, "Component code"), IsActive = true };
    component.Update(name, type, isTaxable);
    return component;
  }

  public void Update(string name, ComponentType type, bool isTaxable)
  {
    if (IsSystem && SystemComponents.TypeOf(ComponentCode) != type)
      throw new DomainException($"{ComponentCode} is a {EnumText.Words(SystemComponents.TypeOf(ComponentCode))} the payroll engine depends on; its type cannot change.");

    if (type == ComponentType.Deduction && isTaxable)
      throw new DomainException("A deduction is not taxable income.");

    ComponentName = Guard.RequiredText(name, 150, "Component name");
    ComponentType = type;
    IsTaxable = isTaxable;
  }

  public void SetActive(bool isActive)
  {
    if (!isActive && IsSystem)
      throw new DomainException($"{ComponentCode} is used by the payroll engine and cannot be deactivated.");

    IsActive = isActive;
  }

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Salary component '{ComponentName}' is inactive.");
  }
}

/// Components whose amount the payroll engine works out itself (not from salary rules).
public static class SystemComponents
{
  public const string BasicPay = "BASIC";
  public const string IncomeTax = "IT";
  public const string GpFund = "GPF";

  public static bool IsSystem(string code) => code is BasicPay or IncomeTax or GpFund;

  public static ComponentType TypeOf(string code) => code == BasicPay ? ComponentType.Earning : ComponentType.Deduction;
}

/// What a salary rule's percentage or tier is applied to.
public static class CalculationBases
{
  public const string BasicPay = "basic_pay";
  public const string MinBasicPay = "min_basic_pay";
  public const string MaxBasicPay = "max_basic_pay";
  public const string GrossPay = "gross_pay";

  public static readonly IReadOnlyList<string> All = [BasicPay, MinBasicPay, MaxBasicPay, GrossPay];
}

/// Who a rule applies to and when; null scope fields match everyone.
public sealed record RuleScope(int Bps, DesignationId DesignationId, OrganizationUnitId OrgUnitId, EmploymentType EmploymentType, DateOnly Date);

public sealed record SalaryRuleDefinition(
  string RuleVersion,
  CalculationMethod CalculationMethod,
  decimal? FixedAmount,
  decimal? Percentage,
  string? CalculationBase,
  string? FormulaExpression,
  int? MinBps,
  int? MaxBps,
  DesignationId? ApplicableDesignationId,
  OrganizationUnitId? ApplicableOrgUnitId,
  EmploymentType? ApplicableEmploymentType,
  decimal? MinAmount,
  decimal? MaxAmount,
  int Priority,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo);

/// The figures a rule is worked out from (monthly, before proration).
public sealed record PayFigures(decimal BasicPay, decimal MinBasicPay, decimal MaxBasicPay, decimal GrossPay, int Bps, int Stage, decimal DaysPayable, int DaysInPeriod)
{
  public decimal Base(string? calculationBase) => calculationBase switch
  {
    CalculationBases.BasicPay => BasicPay,
    CalculationBases.MinBasicPay => MinBasicPay,
    CalculationBases.MaxBasicPay => MaxBasicPay,
    CalculationBases.GrossPay => GrossPay,
    _ => throw new DomainException($"'{calculationBase}' is not a calculation base.")
  };

  public IReadOnlyDictionary<string, decimal> Variables => new Dictionary<string, decimal>(StringComparer.Ordinal)
  {
    ["basic_pay"] = BasicPay,
    ["min_basic_pay"] = MinBasicPay,
    ["max_basic_pay"] = MaxBasicPay,
    ["gross_pay"] = GrossPay,
    ["bps"] = Bps,
    ["stage"] = Stage,
    ["days_payable"] = DaysPayable,
    ["days_in_period"] = DaysInPeriod
  };
}

/// A rule's result for one month: the amount and how it was reached (copied onto the pay-slip line).
public sealed record RuleAmount(decimal Amount, string? CalculationBase, decimal? BaseAmount, decimal? Rate, string? FormulaReference);

/// A versioned, scoped way to work out one component: fixed, a percentage of a base, a formula or tiers. Several rules
/// of one component may be in force at once for different scopes (BPS range, designation, unit, employment type); the
/// lowest priority number wins, then the most specific scope, then the latest start.
public class SalaryComponentRule : Aggregate<SalaryComponentRuleId>
{
  public SalaryComponentId SalaryComponentId { get; private set; } = default!;
  public string RuleVersion { get; private set; } = default!;
  public CalculationMethod CalculationMethod { get; private set; }
  public decimal? FixedAmount { get; private set; }
  public decimal? Percentage { get; private set; }
  public string? CalculationBase { get; private set; }
  public string? FormulaExpression { get; private set; }
  public int? MinBps { get; private set; }
  public int? MaxBps { get; private set; }
  public DesignationId? ApplicableDesignationId { get; private set; }
  public OrganizationUnitId? ApplicableOrgUnitId { get; private set; }
  public EmploymentType? ApplicableEmploymentType { get; private set; }
  public decimal? MinAmount { get; private set; }
  public decimal? MaxAmount { get; private set; }
  public int Priority { get; private set; }
  public string? NotificationRef { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public RecordStatus Status { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  /// How many scope fields are set: a rule for "BPS 17-22 in the Engineering wing" beats one for "everyone".
  public int Specificity =>
      (MinBps.HasValue || MaxBps.HasValue ? 1 : 0) + (ApplicableDesignationId is null ? 0 : 1)
      + (ApplicableOrgUnitId is null ? 0 : 1) + (ApplicableEmploymentType is null ? 0 : 1);

  public static SalaryComponentRule Create(SalaryComponentRuleId id, SalaryComponent component, SalaryRuleDefinition definition, Guid? approvedBy)
  {
    ArgumentNullException.ThrowIfNull(component);
    component.EnsureActive();

    if (component.IsSystem)
      throw new DomainException(component.ComponentCode switch
      {
        SystemComponents.BasicPay => "Basic pay comes from each employee's pay record, not from a rule.",
        SystemComponents.IncomeTax => "Income tax is worked out from the tax slabs, not from a rule.",
        _ => "GP Fund subscription is set on each employee's GP Fund account, not by a rule."
      });

    var rule = new SalaryComponentRule
    {
      Id = id,
      SalaryComponentId = component.Id,
      Status = RecordStatus.Active,
      ApprovedBy = approvedBy
    };
    rule.Apply(component, definition);
    return rule;
  }

  /// Rules are versioned: a rule already used on a pay slip is closed and replaced by a new version, not edited.
  public void Update(SalaryComponent component, SalaryRuleDefinition definition)
  {
    if (component.Id != SalaryComponentId)
      throw new DomainException("A rule cannot move to another component.");

    Apply(component, definition);
  }

  public void CloseOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  public void SetStatus(RecordStatus status) => Status = status;

  public bool Matches(RuleScope scope) =>
      Status == RecordStatus.Active
      && Range.Contains(scope.Date)
      && (MinBps is null || scope.Bps >= MinBps)
      && (MaxBps is null || scope.Bps <= MaxBps)
      && (ApplicableDesignationId is null || ApplicableDesignationId == scope.DesignationId)
      && (ApplicableOrgUnitId is null || ApplicableOrgUnitId == scope.OrgUnitId)
      && (ApplicableEmploymentType is null || ApplicableEmploymentType == scope.EmploymentType);

  /// The monthly amount, clamped to the rule's minimum and maximum.
  public RuleAmount Calculate(PayFigures figures)
  {
    var result = CalculationMethod switch
    {
      CalculationMethod.Fixed => new RuleAmount(FixedAmount!.Value, null, null, null, null),
      CalculationMethod.Percentage => Percent(figures),
      CalculationMethod.Formula => new RuleAmount(PayFormula.Parse(FormulaExpression).Evaluate(figures.Variables), null, null, null, FormulaExpression),
      CalculationMethod.Tiered => Tiered(figures),
      _ => throw new DomainException($"Unknown calculation method {CalculationMethod}.")
    };

    var amount = result.Amount;
    if (MinAmount is { } min && amount < min) amount = min;
    if (MaxAmount is { } max && amount > max) amount = max;
    if (amount < 0) amount = 0;

    return result with { Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero) };
  }

  private RuleAmount Percent(PayFigures figures)
  {
    var baseAmount = figures.Base(CalculationBase);
    return new RuleAmount(baseAmount * Percentage!.Value / 100m, CalculationBase, baseAmount, Percentage, null);
  }

  private RuleAmount Tiered(PayFigures figures)
  {
    var baseAmount = figures.Base(CalculationBase);
    return new RuleAmount(PayTiers.Parse(FormulaExpression).Evaluate(baseAmount), CalculationBase, baseAmount, null, FormulaExpression);
  }

  private void Apply(SalaryComponent component, SalaryRuleDefinition d)
  {
    ArgumentNullException.ThrowIfNull(d);
    DateRange.EnsureValid(d.EffectiveFrom, d.EffectiveTo);

    var minBps = Guard.Between(d.MinBps, 1, 22, "Lowest BPS");
    var maxBps = Guard.Between(d.MaxBps, 1, 22, "Highest BPS");
    if (minBps.HasValue && maxBps.HasValue && minBps > maxBps)
      throw new DomainException("The lowest BPS cannot be above the highest BPS.");

    if (d.MinAmount.HasValue && d.MaxAmount.HasValue && d.MinAmount > d.MaxAmount)
      throw new DomainException("The minimum amount cannot be above the maximum.");

    var calculationBase = d.CalculationBase?.Trim().ToLowerInvariant();
    decimal? fixedAmount = null, percentage = null;
    string? formula = null;

    switch (d.CalculationMethod)
    {
      case CalculationMethod.Fixed:
        fixedAmount = Guard.Money(d.FixedAmount ?? throw new DomainException("A fixed rule needs its amount."), "Fixed amount");
        calculationBase = null;
        break;

      case CalculationMethod.Percentage:
        percentage = Guard.Between(d.Percentage ?? throw new DomainException("A percentage rule needs its percentage."), 0, 1000, "Percentage");
        EnsureBase(component, calculationBase ?? throw new DomainException("A percentage rule needs a calculation base."));
        break;

      case CalculationMethod.Formula:
        var parsed = PayFormula.Parse(d.FormulaExpression);
        if (component.ComponentType == ComponentType.Earning && parsed.UsedVariables.Contains(CalculationBases.GrossPay))
          throw new DomainException("An earning cannot be worked out from gross pay, which it is part of.");
        formula = parsed.Expression;
        calculationBase = null;
        break;

      case CalculationMethod.Tiered:
        EnsureBase(component, calculationBase ?? throw new DomainException("A tiered rule needs a calculation base."));
        PayTiers.Parse(d.FormulaExpression);
        formula = d.FormulaExpression!.Trim();
        break;
    }

    RuleVersion = Guard.RequiredText(d.RuleVersion, 50, "Rule version");
    CalculationMethod = d.CalculationMethod;
    FixedAmount = fixedAmount;
    Percentage = percentage;
    CalculationBase = calculationBase;
    FormulaExpression = formula;
    MinBps = minBps;
    MaxBps = maxBps;
    ApplicableDesignationId = d.ApplicableDesignationId;
    ApplicableOrgUnitId = d.ApplicableOrgUnitId;
    ApplicableEmploymentType = d.ApplicableEmploymentType;
    MinAmount = Guard.Money(d.MinAmount, "Minimum amount");
    MaxAmount = Guard.Money(d.MaxAmount, "Maximum amount");
    Priority = d.Priority;
    NotificationRef = Guard.Text(d.NotificationRef, 200, "Notification");
    EffectiveFrom = d.EffectiveFrom;
    EffectiveTo = d.EffectiveTo;
  }

  private static void EnsureBase(SalaryComponent component, string calculationBase)
  {
    if (!CalculationBases.All.Contains(calculationBase))
      throw new DomainException($"The calculation base must be one of {string.Join(", ", CalculationBases.All)}.");

    if (component.ComponentType == ComponentType.Earning && calculationBase == CalculationBases.GrossPay)
      throw new DomainException("An earning cannot be worked out from gross pay, which it is part of.");
  }

  /// The rule that applies: lowest priority number, then most specific, then the latest start.
  public static SalaryComponentRule? Pick(IEnumerable<SalaryComponentRule> rules, RuleScope scope) =>
      rules.Where(r => r.Matches(scope))
        .OrderBy(r => r.Priority)
        .ThenByDescending(r => r.Specificity)
        .ThenByDescending(r => r.EffectiveFrom)
        .FirstOrDefault();
}

/// An employee-specific override of a component: a fixed amount or a percentage (of the rule's base, or of basic pay
/// when no rule applies), optionally only while the employee holds one post. Periods of one component never overlap.
public class EmployeeSalaryComponent : Aggregate<SalaryOverrideId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public SalaryComponentId SalaryComponentId { get; private set; } = default!;
  public PostId? PostId { get; private set; }
  public decimal? OverrideFixedAmount { get; private set; }
  public decimal? OverridePercentage { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public string? Remarks { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  /// `existing` = the employee's other overrides of the same component.
  public static EmployeeSalaryComponent Create(
      SalaryOverrideId id,
      EmployeeId employeeId,
      SalaryComponent component,
      PostId? postId,
      decimal? fixedAmount,
      decimal? percentage,
      DateOnly effectiveFrom,
      DateOnly? effectiveTo,
      string? remarks,
      IReadOnlyCollection<EmployeeSalaryComponent> existing)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(component);
    component.EnsureActive();

    if (component.IsSystem)
      throw new DomainException(component.ComponentCode switch
      {
        SystemComponents.BasicPay => "Basic pay cannot be overridden: change the employee's pay record instead.",
        SystemComponents.IncomeTax => "Income tax cannot be overridden: it comes from the tax slabs (record an exemption instead).",
        _ => "GP Fund cannot be overridden: change the subscription on the employee's GP Fund account instead."
      });

    var item = new EmployeeSalaryComponent { Id = id, EmployeeId = employeeId, SalaryComponentId = component.Id, PostId = postId };
    item.Apply(fixedAmount, percentage, effectiveFrom, effectiveTo, remarks, existing);
    return item;
  }

  public void Update(decimal? fixedAmount, decimal? percentage, DateOnly effectiveFrom, DateOnly? effectiveTo, string? remarks, IReadOnlyCollection<EmployeeSalaryComponent> existing) =>
      Apply(fixedAmount, percentage, effectiveFrom, effectiveTo, remarks, existing);

  public void EndOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  /// The overriding monthly amount: fixed, or the percentage of the base the rule would use (basic pay without a rule).
  public RuleAmount Calculate(PayFigures figures, SalaryComponentRule? rule)
  {
    if (OverrideFixedAmount is { } amount)
      return new RuleAmount(amount, null, null, null, null);

    var calculationBase = rule?.CalculationBase ?? CalculationBases.BasicPay;
    var baseAmount = figures.Base(calculationBase);
    return new RuleAmount(decimal.Round(baseAmount * OverridePercentage!.Value / 100m, 2, MidpointRounding.AwayFromZero), calculationBase, baseAmount, OverridePercentage, null);
  }

  private void Apply(decimal? fixedAmount, decimal? percentage, DateOnly effectiveFrom, DateOnly? effectiveTo, string? remarks, IReadOnlyCollection<EmployeeSalaryComponent> existing)
  {
    if (fixedAmount.HasValue == percentage.HasValue)
      throw new DomainException("An override is either a fixed amount or a percentage.");

    DateRange.EnsureValid(effectiveFrom, effectiveTo);
    var range = new DateRange(effectiveFrom, effectiveTo);

    if (existing.FirstOrDefault(e => e.Id != Id && e.EmployeeId == EmployeeId && e.SalaryComponentId == SalaryComponentId && e.Range.Overlaps(range)) is { } clash)
      throw new DomainException($"Another override of this component runs from {clash.EffectiveFrom:yyyy-MM-dd}; end it first.");

    OverrideFixedAmount = Guard.Money(fixedAmount, "Override amount");
    OverridePercentage = Guard.Between(percentage, 0, 1000, "Override percentage");
    EffectiveFrom = effectiveFrom;
    EffectiveTo = effectiveTo;
    Remarks = Guard.Text(remarks, 2000, "Remarks");
  }
}
