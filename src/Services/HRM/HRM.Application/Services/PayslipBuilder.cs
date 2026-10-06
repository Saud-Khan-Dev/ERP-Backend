using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

/// Builds pay slips: the snapshot frozen when a run is finalized (so later changes to the employee, the post or the
/// bank account never rewrite an issued slip), and the same view as a preview before that.
public class PayslipBuilder(IApplicationDbContext context, HrLookup lookup)
{
  public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

  public static string Serialize(PayslipDto slip) => JsonSerializer.Serialize(slip, Json);

  public static PayslipDto Deserialize(string json) =>
      JsonSerializer.Deserialize<PayslipDto>(json, Json) ?? throw new InvalidOperationException("The pay slip snapshot is empty.");

  /// `slips` must carry their segments, lines, loan deductions and adjustments.
  public async Task<Dictionary<PayrollTransactionId, PayslipDto>> BuildAsync(
      PayrollRun run,
      PayrollPeriod period,
      IReadOnlyCollection<PayrollTransaction> slips,
      bool isFinal,
      DateTime? issuedAt,
      CancellationToken cancellationToken)
  {
    var employeeIds = slips.Select(s => s.EmployeeId).Distinct().ToList();
    var employeeGuids = employeeIds.Select(e => e.Value).ToList();
    var employees = await context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
    var components = await context.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

    // where each employee sat at the end of the slip
    var lastSegments = slips.Where(s => s.Segments.Count > 0).ToDictionary(s => s.Id, s => s.Segments.OrderBy(x => x.PeriodFrom).Last());
    var posts = new Dictionary<(Guid PostId, DateOnly Date), HrLookup.PostOnDate>();
    foreach (var group in lastSegments.Values.GroupBy(s => s.PeriodTo))
      foreach (var (postId, post) in await lookup.PostsOnAsync(group.Select(s => s.PostId), group.Key, cancellationToken))
        posts[(postId, group.Key)] = post;
    var stageIds = lastSegments.Values.Where(s => s.PayScaleStageId is not null).Select(s => s.PayScaleStageId!).Distinct().ToList();
    var stages = await context.PayScaleStages.AsNoTracking().Where(s => stageIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.StageNumber, cancellationToken);

    // the loans recovered on the slips, with what is still owed
    var loanIds = slips.SelectMany(s => s.LoanDeductions).Select(d => d.EmployeeLoanId).Distinct().ToList();
    var loans = await context.Loans.AsNoTracking().Include(l => l.Installments).Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
    var loanTypes = await context.LoanTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

    var accounts = await context.GpFundAccounts.AsNoTracking().Where(a => employeeIds.Contains(a.EmployeeId))
      .Select(a => new { a.Id, a.EmployeeId }).ToListAsync(cancellationToken);
    var accountGuids = accounts.Select(a => a.Id.Value).ToList();
    var balances = await context.GpFundBalances.AsNoTracking().Where(b => accountGuids.Contains(b.GpFundAccountId))
      .ToDictionaryAsync(b => b.GpFundAccountId, b => b.Balance, cancellationToken);
    var gpf = accounts.ToDictionary(a => a.EmployeeId, a => balances.GetValueOrDefault(a.Id.Value));

    var taxYear = await context.TaxYears.AsNoTracking()
      .FirstOrDefaultAsync(t => t.Status == RecordStatus.Active && t.StartDate <= period.EndDate && t.EndDate >= period.EndDate, cancellationToken);
    var ytd = taxYear is null
      ? new Dictionary<Guid, EmployeeTaxYtd>()
      : await context.EmployeeTaxYtd.AsNoTracking().Where(t => t.TaxYearId == taxYear.Id.Value && employeeGuids.Contains(t.EmployeeId))
        .ToDictionaryAsync(t => t.EmployeeId, cancellationToken);
    var banks = await context.BankAccounts.AsNoTracking()
      .Where(a => employeeIds.Contains(a.EmployeeId) && a.IsPrimary && a.Status == RecordStatus.Active)
      .ToDictionaryAsync(a => a.EmployeeId, cancellationToken);

    var result = new Dictionary<PayrollTransactionId, PayslipDto>();
    foreach (var slip in slips)
    {
      var employee = employees[slip.EmployeeId];
      var segment = lastSegments.GetValueOrDefault(slip.Id);
      var post = segment is null ? null : posts.GetValueOrDefault((segment.PostId.Value, segment.PeriodTo));
      var bank = banks.GetValueOrDefault(slip.EmployeeId);
      var tax = ytd.GetValueOrDefault(slip.EmployeeId.Value);

      result[slip.Id] = new PayslipDto(
        slip.Id.Value, run.Id.Value, period.Label, period.Year, period.Month, run.RunType, run.RunLabel,
        employee.Id.Value, employee.EmployeeNumber, employee.DisplayName, employee.Cnic,
        post?.PostCode, post?.Designation, post?.Bps, segment?.PayScaleStageId is { } stage ? stages.GetValueOrDefault(stage) : null, post?.OrgUnit,
        slip.DaysPayable, period.Days,
        Earnings(slip, components), Deductions(slip, components),
        slip.GrossPay, slip.TotalDeductions, slip.NetPayable,
        Loans(slip, loans, loanTypes),
        gpf.TryGetValue(slip.EmployeeId, out var fund) ? fund : null,
        taxYear?.YearLabel, tax?.TaxableIncomeYtd, tax?.TaxWithheldYtd,
        bank?.BankName, bank?.AccountNumber, bank?.Iban,
        slip.Remarks, isFinal, issuedAt);
    }
    return result;
  }

  private static List<PayslipLineDto> Earnings(PayrollTransaction slip, Dictionary<SalaryComponentId, SalaryComponent> components)
  {
    var lines = slip.Lines.Where(l => l.ComponentType == ComponentType.Earning)
      .GroupBy(l => l.SalaryComponentId)
      .Select(g => Line(components[g.Key], g.ToList()))
      .OrderBy(l => l.ComponentCode == SystemComponents.BasicPay ? 0 : 1).ThenBy(l => l.ComponentName)
      .ToList();

    lines.AddRange(slip.Adjustments.Where(a => a.Amount > 0)
      .Select(a => new PayslipLineDto("ADJ", EnumText.Label(a.AdjustmentType), a.Amount, a.Reason)));
    return lines;
  }

  private static List<PayslipLineDto> Deductions(PayrollTransaction slip, Dictionary<SalaryComponentId, SalaryComponent> components)
  {
    static int Order(ComponentSource source) => source switch
    {
      ComponentSource.Rule or ComponentSource.Override => 0,
      ComponentSource.Gpf => 1,
      ComponentSource.Loan => 2,
      _ => 3
    };

    var lines = slip.Lines.Where(l => l.ComponentType == ComponentType.Deduction)
      .OrderBy(l => Order(l.Source)).ThenBy(l => components[l.SalaryComponentId].ComponentName)
      .Select(l => Line(components[l.SalaryComponentId], [l]))
      .ToList();

    lines.AddRange(slip.Adjustments.Where(a => a.Amount < 0)
      .Select(a => new PayslipLineDto("ADJ", EnumText.Label(a.AdjustmentType), -a.Amount, a.Reason)));
    return lines;
  }

  /// One component's line; the detail says how a single line was reached.
  private static PayslipLineDto Line(SalaryComponent component, List<PayrollComponentDetail> lines)
  {
    string? detail = null;
    if (lines.Count == 1)
    {
      var line = lines[0];
      detail = line.Source switch
      {
        ComponentSource.Loan => line.FormulaReference,
        ComponentSource.Tax => line.BaseAmount is { } taxable ? $"On taxable pay of {taxable:N2}" : null,
        _ when line.Rate is { } rate && line.BaseAmount is { } baseAmount => $"{rate:0.##}% of {baseAmount:N2}",
        _ => null
      };
    }
    else if (lines.Count > 1)
    {
      detail = $"{lines.Count} parts of the month";
    }

    return new PayslipLineDto(component.ComponentCode, component.ComponentName, lines.Sum(l => l.CalculatedAmount), detail);
  }

  private static List<PayslipLoanDto> Loans(PayrollTransaction slip, Dictionary<EmployeeLoanId, EmployeeLoan> loans, Dictionary<LoanTypeId, string> types) =>
      slip.LoanDeductions.Where(d => loans.ContainsKey(d.EmployeeLoanId)).Select(d =>
      {
        var loan = loans[d.EmployeeLoanId];
        var installment = loan.Installments.FirstOrDefault(i => i.Id == d.InstallmentId);
        return new PayslipLoanDto(loan.Id.Value, types.GetValueOrDefault(loan.LoanTypeId) ?? "Loan",
          installment is null ? "Installment" : $"Installment {installment.InstallmentNumber} of {loan.InstallmentsCount}", d.InstallmentAmount, loan.RemainingBalance);
      }).ToList();
}
