/// A regular post held during the period (from the employee's position assignments).
public sealed record AssignmentSlice(PostId PostId, DateOnly From, DateOnly? To)
{
  public DateRange Range => new(From, To);
}

/// What a post was during part of the period.
public sealed record PostVersionSlice(PostId PostId, PayScaleGradeId GradeId, int Bps, DesignationId DesignationId, OrganizationUnitId OrgUnitId, DateOnly From, DateOnly? To)
{
  public DateRange Range => new(From, To);
}

/// The employee's pay position during part of the period, with the scale it sits in.
public sealed record PaySlice(PayScaleStageId StageId, int StageNumber, decimal BasicPay, decimal MinBasicPay, decimal MaxBasicPay, DateOnly From, DateOnly? To)
{
  public DateRange Range => new(From, To);
}

/// An open loan installment due by the end of the period.
public sealed record DueInstallment(EmployeeLoanId LoanId, LoanInstallmentId InstallmentId, SalaryComponentId ComponentId, int Priority, int InstallmentNumber, DateOnly DueDate, decimal Amount);

/// Tax position of the employee for the tax year the period falls in. Year-to-date figures exclude the run being
/// calculated. RecurringMonthlyTaxable = a normal full month's taxable pay (for runs that are not computed by the engine).
public sealed record TaxInput(TaxYear Year, decimal Exemptions, decimal YtdTaxable, decimal YtdWithheld, int MonthsRemaining, decimal RecurringMonthlyTaxable);

public sealed record PayrollEmployeeInput(
  EmployeeId EmployeeId,
  EmploymentType EmploymentType,
  IReadOnlyList<AssignmentSlice> Assignments,
  IReadOnlyList<PostVersionSlice> PostVersions,
  IReadOnlyList<PaySlice> PayRecords,
  IReadOnlyList<DateRange> UnpaidLeave,
  IReadOnlyList<EmployeeSalaryComponent> Overrides,
  decimal GpfSubscription,
  IReadOnlyList<DueInstallment> Installments,
  decimal TaxableAdjustments,
  decimal NetAdjustments,
  TaxInput? Tax);

/// The salary components and rules in force, shared by every employee of a run.
public sealed record PayrollCatalog(
  IReadOnlyList<SalaryComponent> Components,
  IReadOnlyList<SalaryComponentRule> Rules,
  SalaryComponent BasicPay,
  SalaryComponent IncomeTax,
  SalaryComponent? GpFund,
  IReadOnlySet<SalaryComponentId> LoanComponents);

/// The payroll engine for one employee in one run. Pure: everything it needs comes in, the pay slip comes out.
///
/// - The period is split into segments wherever the regular post, the post's version (grade, unit) or the pay record
///   changes. Days without a regular post are not paid.
/// - Earnings are monthly amounts prorated by payable days / days in the period; unpaid leave (LWOP) is not payable.
///   Basic pay comes from the pay record; every other earning from the employee's override or the best-matching rule,
///   split again where a rule or override starts or ends inside the segment.
/// - Deductions are whole-month amounts on the last segment's scope: rule deductions, GP Fund subscription, loan
///   installments due, then income tax (annualized over the tax year).
/// - If the deductions would take net pay below zero, loan installments are left for next month (least important
///   first), then GP Fund, then rule deductions.
public static class PayrollCalculator
{
  public static PayCalculation Calculate(DateRange period, bool isComputedRun, PayrollEmployeeInput input, PayrollCatalog catalog)
  {
    var notes = new List<string>();
    var periodDays = period.Days;

    if (!isComputedRun)
      return AdjustmentsOnly(input, catalog, notes);

    var segments = BuildSegments(period, input, notes);
    var lines = new List<LineResult>();
    decimal fullMonthTaxable = 0;

    foreach (var segment in segments)
    {
      var basic = Prorate(segment.Pay.BasicPay, segment.Result.PayableDays, periodDays);
      if (basic > 0)
        lines.Add(new LineResult(segment.Result.Index, catalog.BasicPay.Id, null, ComponentSource.Rule, ComponentType.Earning,
          CalculationBases.BasicPay, segment.Pay.BasicPay, null, null, basic, null, segment.Result.From));

      var isLast = segment == segments[^1];
      if (isLast && catalog.BasicPay.IsTaxable)
        fullMonthTaxable += segment.Pay.BasicPay;

      foreach (var component in catalog.Components.Where(c => c.IsActive && c.ComponentType == ComponentType.Earning && !c.IsSystem))
      {
        foreach (var piece in Pieces(segment.Result.From, segment.Result.To, component, catalog.Rules, input.Overrides))
        {
          var scope = new RuleScope(segment.Post.Bps, segment.Post.DesignationId, segment.Post.OrgUnitId, input.EmploymentType, piece.From);
          var payable = PayableDays(piece.From, piece.To, input.UnpaidLeave);
          var amount = Amount(component, scope, segment, payable, periodDays, 0, input.Overrides, catalog.Rules);
          if (amount is null)
            continue;

          var (monthly, rule, @override) = amount.Value;
          var prorated = Prorate(monthly.Amount, payable, periodDays);
          if (prorated > 0)
            lines.Add(new LineResult(segment.Result.Index, component.Id, rule?.Id, @override is null ? ComponentSource.Rule : ComponentSource.Override,
              ComponentType.Earning, monthly.CalculationBase, monthly.BaseAmount, monthly.Rate, monthly.FormulaReference, prorated,
              rule?.NotificationRef, @override?.EffectiveFrom ?? rule?.EffectiveFrom));

          if (isLast && piece.To == segment.Result.To && component.IsTaxable)
            fullMonthTaxable += monthly.Amount;
        }
      }
    }

    var daysPayable = segments.Sum(s => s.Result.PayableDays);
    var grossEarnings = lines.Where(l => l.ComponentType == ComponentType.Earning).Sum(l => l.Amount);
    var deductionLines = new List<LineResult>();

    if (segments.Count > 0 && daysPayable > 0)
    {
      var last = segments[^1];
      var scope = new RuleScope(last.Post.Bps, last.Post.DesignationId, last.Post.OrgUnitId, input.EmploymentType, last.Result.To);
      var grossForDeductions = grossEarnings + input.TaxableAdjustments;

      foreach (var component in catalog.Components.Where(c => c.IsActive && c.ComponentType == ComponentType.Deduction && !c.IsSystem && !catalog.LoanComponents.Contains(c.Id)))
      {
        var amount = Amount(component, scope, last, daysPayable, periodDays, grossForDeductions, input.Overrides, catalog.Rules);
        if (amount is null || amount.Value.Amount.Amount <= 0)
          continue;

        var (monthly, rule, @override) = amount.Value;
        deductionLines.Add(new LineResult(null, component.Id, rule?.Id, @override is null ? ComponentSource.Rule : ComponentSource.Override,
          ComponentType.Deduction, monthly.CalculationBase, monthly.BaseAmount, monthly.Rate, monthly.FormulaReference, monthly.Amount,
          rule?.NotificationRef, @override?.EffectiveFrom ?? rule?.EffectiveFrom));
      }

      if (input.GpfSubscription > 0)
      {
        if (catalog.GpFund is { IsActive: true } gpf)
          deductionLines.Add(new LineResult(null, gpf.Id, null, ComponentSource.Gpf, ComponentType.Deduction, null, null, null, null,
            decimal.Round(input.GpfSubscription, 2), null, period.From));
        else
          notes.Add("GP Fund subscription not deducted: the GPF salary component is missing or inactive.");
      }

      foreach (var installment in input.Installments.OrderBy(i => i.Priority).ThenBy(i => i.DueDate).ThenBy(i => i.InstallmentNumber))
      {
        deductionLines.Add(new LineResult(null, installment.ComponentId, null, ComponentSource.Loan, ComponentType.Deduction, null, null, null,
          $"Installment {installment.InstallmentNumber} due {installment.DueDate:yyyy-MM-dd}", installment.Amount, null, installment.DueDate,
          new LoanDeductionResult(installment.LoanId, installment.InstallmentId, installment.Amount)));
      }
    }
    else if (input.Installments.Count > 0)
    {
      notes.Add("No payable days this month: loan installments are left for a later month.");
    }

    var taxable = lines.Where(l => l.ComponentType == ComponentType.Earning && IsTaxable(l.ComponentId, catalog)).Sum(l => l.Amount) + input.TaxableAdjustments;
    var tax = input.Tax is { } taxInput && taxable > 0
      ? MonthlyTax(taxInput, taxable, fullMonthTaxable)
      : 0m;

    if (tax > 0)
      deductionLines.Add(new LineResult(null, catalog.IncomeTax.Id, null, ComponentSource.Tax, ComponentType.Deduction, null, taxable, null, null, tax, null, period.To));

    KeepNetPositive(grossEarnings + input.TaxableAdjustments, input.NetAdjustments - input.TaxableAdjustments, deductionLines, notes);

    lines.AddRange(deductionLines);
    return new PayCalculation(daysPayable, segments.Select(s => s.Result).ToList(), lines, taxable, tax, notes);
  }

  /// Arrears, bonus and final-settlement runs: the pay is the adjustments entered; the engine only withholds the extra
  /// income tax they cause.
  private static PayCalculation AdjustmentsOnly(PayrollEmployeeInput input, PayrollCatalog catalog, List<string> notes)
  {
    var lines = new List<LineResult>();
    var taxable = input.TaxableAdjustments;
    decimal tax = 0;

    if (input.Tax is { } t && taxable > 0)
    {
      var baseIncome = Math.Max(0, t.YtdTaxable + t.RecurringMonthlyTaxable * t.MonthsRemaining - t.Exemptions);
      tax = decimal.Round(Math.Max(0, t.Year.AnnualTax(baseIncome + taxable) - t.Year.AnnualTax(baseIncome)), 2, MidpointRounding.AwayFromZero);
      if (tax > 0)
        lines.Add(new LineResult(null, catalog.IncomeTax.Id, null, ComponentSource.Tax, ComponentType.Deduction, null, taxable, null, null, tax, null, null));
    }

    KeepNetPositive(input.TaxableAdjustments, input.NetAdjustments - input.TaxableAdjustments, lines, notes);
    return new PayCalculation(0, [], lines, taxable, tax, notes);
  }

  /// Monthly withholding: the tax on the projected annual income, less what was already withheld, spread over the
  /// months left in the tax year (this one included).
  public static decimal MonthlyTax(TaxInput tax, decimal currentTaxable, decimal recurringMonthlyTaxable)
  {
    var months = Math.Max(1, tax.MonthsRemaining);
    var projected = tax.YtdTaxable + currentTaxable + recurringMonthlyTaxable * (months - 1) - tax.Exemptions;
    var annual = tax.Year.AnnualTax(Math.Max(0, projected));
    var due = (annual - tax.YtdWithheld) / months;
    return decimal.Round(Math.Max(0, due), 2, MidpointRounding.AwayFromZero);
  }

  private sealed record Segment(SegmentResult Result, PostVersionSlice Post, PaySlice Pay);

  private static List<Segment> BuildSegments(DateRange period, PayrollEmployeeInput input, List<string> notes)
  {
    var boundaries = new SortedSet<DateOnly> { period.From };
    void Add(DateOnly from, DateOnly? to)
    {
      if (from > period.From && from <= period.To!.Value) boundaries.Add(from);
      if (to is { } end && end >= period.From && end < period.To!.Value) boundaries.Add(end.AddDays(1));
    }

    foreach (var a in input.Assignments) Add(a.From, a.To);
    foreach (var v in input.PostVersions) Add(v.From, v.To);
    foreach (var p in input.PayRecords) Add(p.From, p.To);

    var points = boundaries.ToList();
    var raw = new List<(DateOnly From, DateOnly To, AssignmentSlice Assignment, PostVersionSlice Post, PaySlice Pay)>();
    for (var i = 0; i < points.Count; i++)
    {
      var from = points[i];
      var to = i + 1 < points.Count ? points[i + 1].AddDays(-1) : period.To!.Value;

      var assignment = input.Assignments.FirstOrDefault(a => a.Range.Contains(from));
      if (assignment is null)
        continue;

      var post = input.PostVersions.FirstOrDefault(v => v.PostId == assignment.PostId && v.Range.Contains(from));
      if (post is null)
      {
        notes.Add($"No post version in effect on {from:yyyy-MM-dd}; those days are not paid.");
        continue;
      }

      var pay = input.PayRecords.FirstOrDefault(p => p.Range.Contains(from));
      if (pay is null)
        throw new DomainException($"The employee has no pay record on {from:yyyy-MM-dd}. Record the pay before running payroll.");

      // consecutive stretches on the same post, version and pay are one segment
      if (raw.Count > 0 && raw[^1].To.AddDays(1) == from && raw[^1].Assignment == assignment && raw[^1].Post == post && raw[^1].Pay == pay)
        raw[^1] = raw[^1] with { To = to };
      else
        raw.Add((from, to, assignment, post, pay));
    }

    return raw.Select((r, index) =>
    {
      var days = r.To.DayNumber - r.From.DayNumber + 1;
      var payable = PayableDays(r.From, r.To, input.UnpaidLeave);
      return new Segment(new SegmentResult(index, r.Post.PostId, r.Post.GradeId, r.Pay.StageId, r.Pay.BasicPay, r.From, r.To, days, payable), r.Post, r.Pay);
    }).ToList();
  }

  /// Sub-ranges of a segment over which one component's rule / override situation is constant.
  private static IEnumerable<(DateOnly From, DateOnly To)> Pieces(DateOnly from, DateOnly to, SalaryComponent component, IReadOnlyList<SalaryComponentRule> rules, IReadOnlyList<EmployeeSalaryComponent> overrides)
  {
    var cuts = new SortedSet<DateOnly> { from };
    void Add(DateOnly start, DateOnly? end)
    {
      if (start > from && start <= to) cuts.Add(start);
      if (end is { } e && e >= from && e < to) cuts.Add(e.AddDays(1));
    }

    foreach (var rule in rules.Where(r => r.SalaryComponentId == component.Id)) Add(rule.EffectiveFrom, rule.EffectiveTo);
    foreach (var item in overrides.Where(o => o.SalaryComponentId == component.Id)) Add(item.EffectiveFrom, item.EffectiveTo);

    var points = cuts.ToList();
    for (var i = 0; i < points.Count; i++)
      yield return (points[i], i + 1 < points.Count ? points[i + 1].AddDays(-1) : to);
  }

  private static (RuleAmount Amount, SalaryComponentRule? Rule, EmployeeSalaryComponent? Override)? Amount(
      SalaryComponent component,
      RuleScope scope,
      Segment segment,
      decimal daysPayable,
      int periodDays,
      decimal grossPay,
      IReadOnlyList<EmployeeSalaryComponent> overrides,
      IReadOnlyList<SalaryComponentRule> rules)
  {
    var rule = SalaryComponentRule.Pick(rules.Where(r => r.SalaryComponentId == component.Id), scope);
    var @override = overrides.FirstOrDefault(o => o.SalaryComponentId == component.Id && o.Range.Contains(scope.Date)
      && (o.PostId is null || o.PostId == segment.Post.PostId));

    if (rule is null && @override is null)
      return null;

    var figures = new PayFigures(segment.Pay.BasicPay, segment.Pay.MinBasicPay, segment.Pay.MaxBasicPay, grossPay,
      segment.Post.Bps, segment.Pay.StageNumber, daysPayable, periodDays);

    var amount = @override is not null ? @override.Calculate(figures, rule) : rule!.Calculate(figures);
    return (amount, rule, @override);
  }

  private static decimal PayableDays(DateOnly from, DateOnly to, IReadOnlyList<DateRange> unpaidLeave)
  {
    var days = to.DayNumber - from.DayNumber + 1;
    var unpaid = new HashSet<DateOnly>();
    foreach (var leave in unpaidLeave)
    {
      if (leave.Clip(from, to) is not { } overlap)
        continue;
      for (var d = overlap.From; d <= overlap.To!.Value; d = d.AddDays(1))
        unpaid.Add(d);
    }
    return days - unpaid.Count;
  }

  private static decimal Prorate(decimal monthly, decimal payableDays, int periodDays) =>
      payableDays <= 0 ? 0 : decimal.Round(monthly * payableDays / periodDays, 2, MidpointRounding.AwayFromZero);

  private static bool IsTaxable(SalaryComponentId componentId, PayrollCatalog catalog) =>
      catalog.Components.FirstOrDefault(c => c.Id == componentId)?.IsTaxable ?? false;

  /// Drops deductions until net pay is not negative: loan installments (least important first), then GP Fund, then
  /// rule deductions. Income tax is never dropped. `negativeAdjustments` is the (negative) total of the recoveries.
  private static void KeepNetPositive(decimal gross, decimal negativeAdjustments, List<LineResult> deductions, List<string> notes)
  {
    decimal Net() => gross + negativeAdjustments - deductions.Sum(d => d.Amount);

    if (Net() >= 0)
      return;

    // loans were added most important first, so the least important are at the end
    var droppable = deductions.Where(d => d.Source == ComponentSource.Loan).Reverse()
      .Concat(deductions.Where(d => d.Source == ComponentSource.Gpf))
      .Concat(deductions.Where(d => d.Source is ComponentSource.Rule or ComponentSource.Override).Reverse())
      .ToList();

    foreach (var line in droppable)
    {
      if (Net() >= 0)
        break;

      deductions.Remove(line);
      notes.Add(line.Source switch
      {
        ComponentSource.Loan => $"{line.FormulaReference} not recovered: the pay does not cover it.",
        ComponentSource.Gpf => "GP Fund subscription not deducted: the pay does not cover it.",
        _ => "A deduction was left out: the pay does not cover it."
      });
    }

    if (Net() < 0)
      notes.Add($"Net pay is negative ({Net():N2}): recover the balance in a later month.");
  }
}
