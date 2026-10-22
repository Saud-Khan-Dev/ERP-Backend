using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One employee's difference for a past month: what the posted payroll paid in earnings, what today's records say was
/// due, and the difference by pay line.
public sealed record ArrearsLineDto(Guid EmployeeId, string EmployeeNumber, string EmployeeName, decimal Paid, decimal Due, decimal Difference, string Breakdown, string? Problem);

public sealed record WorkOutArrearsResult(Guid RunId, string SourcePeriod, int Employees, decimal Arrears, decimal Recoveries, bool DryRun, IReadOnlyList<ArrearsLineDto> Lines);

/// "Recalculate a past month": the earnings of a finalized month are worked out again with today's records (a promotion,
/// an increment or a pay revision dated back, leave without pay approved later, a separation recorded late) and compared
/// with what its posted payroll paid. Each difference becomes an adjustment on the given run - Arrears when more was due,
/// Recovery when less - so the next calculation adds only the tax it causes. Running it again replaces its own
/// adjustments for that month. Loans, GP Fund and tax of the past month are not touched (they were posted).
public sealed record WorkOutArrearsCommand(Guid RunId, Guid SourcePeriodId, IReadOnlyList<Guid>? EmployeeIds, bool DryRun) : ICommand<Result<WorkOutArrearsResult>>;

public class WorkOutArrearsCommandValidator : AbstractValidator<WorkOutArrearsCommand>
{
  public WorkOutArrearsCommandValidator()
  {
    RuleFor(x => x.RunId).NotEmpty();
    RuleFor(x => x.SourcePeriodId).NotEmpty();
  }
}

public class ArrearsHandlers(IApplicationDbContext context, PayrollEngine engine, PayrollInputBuilder inputs, HrLookup lookup, OfficeScope scope, IAuditTrail audit) :
  ICommandHandler<WorkOutArrearsCommand, Result<WorkOutArrearsResult>>
{
  public async Task<Result<WorkOutArrearsResult>> Handle(WorkOutArrearsCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("work out arrears for a payroll run", cancellationToken) is { } refusal)
      return Result<WorkOutArrearsResult>.Failure(refusal);
    var run = await context.LoadPayrollRunAsync(command.RunId, cancellationToken);
    if (run.RunType is not (PayrollRunType.Arrears or PayrollRunType.Regular or PayrollRunType.Supplementary))
      return Result<WorkOutArrearsResult>.Failure("Arrears and recoveries go on an arrears run, or on this month's regular or supplementary run.");
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    if (!command.DryRun)
    {
      period.EnsureOpen();
      run.EnsureEditable();
    }

    var source = await context.LoadPayrollPeriodAsync(command.SourcePeriodId, cancellationToken);
    if (source.EndDate >= period.StartDate)
      return Result<WorkOutArrearsResult>.Failure($"{source.Label} is not before {period.Label}; arrears are worked out for a month already paid.");

    // the month's posted runs that the engine worked out (regular and supplementary)
    var posted = await context.PayrollRuns.AsNoTracking()
      .Where(r => r.PayrollPeriodId == source.Id && (r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid)
        && (r.RunType == PayrollRunType.Regular || r.RunType == PayrollRunType.Supplementary))
      .ToListAsync(cancellationToken);
    if (posted.Count == 0)
      return Result<WorkOutArrearsResult>.Failure($"{source.Label} has no finalized payroll to compare with.");
    var postedIds = posted.Select(r => r.Id).ToList();

    // who: everyone paid that month, and everyone who held a post then (an appointment recorded late)
    var paidIds = await context.PayrollTransactions.AsNoTracking().Where(t => postedIds.Contains(t.PayrollRunId)).Select(t => t.EmployeeId).Distinct().ToListAsync(cancellationToken);
    var heldIds = await inputs.PostedEmployeesAsync(source, cancellationToken);
    var ids = paidIds.Union(heldIds).Distinct().ToList();
    if (command.EmployeeIds is { Count: > 0 } only)
    {
      var wanted = only.Select(EmployeeId.Of).ToHashSet();
      ids = ids.Where(wanted.Contains).ToList();
    }
    var employees = await context.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);

    // what was paid: the earnings lines of the posted slips (adjustments left out)
    var paidLines = await (
        from l in context.PayrollLines.AsNoTracking()
        join t in context.PayrollTransactions on l.PayrollTransactionId equals t.Id
        where postedIds.Contains(t.PayrollRunId) && ids.Contains(t.EmployeeId) && l.ComponentType == ComponentType.Earning && l.Source != ComponentSource.Adjustment
        select new { t.EmployeeId, l.SalaryComponentId, l.CalculatedAmount })
      .ToListAsync(cancellationToken);

    // what was due: the engine on today's records for that month (the first posted regular run stands in for it)
    var basis = posted.OrderBy(r => r.RunType).First();
    var catalog = await inputs.CatalogAsync(source, cancellationToken);
    var data = await inputs.InputsAsync(basis, source, employees, new Dictionary<EmployeeId, PayrollTransaction>(), null, cancellationToken);
    var names = (await context.SalaryComponents.AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.ComponentName);
    var people = await lookup.EmployeesAsync(employees.Select(e => (EmployeeId?)e.Id), cancellationToken);

    var marker = $"Worked out for {source.Label}";
    var lines = new List<ArrearsLineDto>();
    var differences = new Dictionary<EmployeeId, (decimal Amount, string Reason)>();
    foreach (var employee in employees.OrderBy(e => e.EmployeeNumber))
    {
      var person = people.GetValueOrDefault(employee.Id.Value);
      var paid = paidLines.Where(l => l.EmployeeId == employee.Id).GroupBy(l => l.SalaryComponentId).ToDictionary(g => g.Key, g => g.Sum(l => l.CalculatedAmount));
      Dictionary<SalaryComponentId, decimal> due;
      try
      {
        var calculation = PayrollCalculator.Calculate(source.Range, true, data[employee.Id], catalog);
        due = calculation.Lines.Where(l => l.ComponentType == ComponentType.Earning && l.Source != ComponentSource.Adjustment)
          .GroupBy(l => l.ComponentId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
      }
      catch (DomainException error)
      {
        // nothing is due any more (no post, no pay record): everything paid is to be recovered
        due = [];
        if (paid.Count == 0)
        {
          lines.Add(new(employee.Id.Value, employee.EmployeeNumber, person?.FullName ?? employee.DisplayName, 0, 0, 0, "", DomainMessages.Text(error)));
          continue;
        }
      }

      var parts = paid.Keys.Union(due.Keys)
        .Select(c => (Component: c, Diff: decimal.Round(due.GetValueOrDefault(c) - paid.GetValueOrDefault(c), 2)))
        .Where(x => x.Diff != 0).ToList();
      var total = parts.Sum(x => x.Diff);
      var breakdown = string.Join("; ", parts.Select(x => $"{names.GetValueOrDefault(x.Component, "Pay")} {(x.Diff > 0 ? "+" : "")}{x.Diff:#,##0.00}"));
      lines.Add(new(employee.Id.Value, employee.EmployeeNumber, person?.FullName ?? employee.DisplayName, paid.Values.Sum(), due.Values.Sum(), total, breakdown, null));
      if (total != 0)
        differences[employee.Id] = (total, $"{marker}: {breakdown}".Length > 1900 ? $"{marker}." : $"{marker}: {breakdown}");
    }

    if (!command.DryRun)
    {
      using var quiet = audit.Quietly();
      // replace what an earlier working-out of the same month left on this run
      var slips = await engine.SlipsAsync(run.Id, cancellationToken);
      foreach (var slip in slips)
        foreach (var old in slip.Adjustments.Where(a => a.Reason != null && a.Reason.StartsWith(marker, StringComparison.Ordinal)).ToList())
          slip.RemoveAdjustment(old.Id);

      foreach (var (employeeId, (amount, reason)) in differences)
      {
        var slip = slips.FirstOrDefault(s => s.EmployeeId == employeeId);
        if (slip is null)
        {
          slip = PayrollTransaction.Open(PayrollTransactionId.New(), run, employees.First(e => e.Id == employeeId));
          context.PayrollTransactions.Add(slip);
          slips.Add(slip);
        }
        slip.AddAdjustment(amount > 0 ? AdjustmentType.Arrears : AdjustmentType.Recovery, amount, reason);
      }

      var touched = slips.Where(s => differences.ContainsKey(s.EmployeeId) || s.Adjustments.Count == 0).Select(s => s.EmployeeId).ToHashSet();
      var calculate = await context.Employees.AsNoTracking().Where(e => touched.Contains(e.Id)).ToListAsync(cancellationToken);
      foreach (var problem in await engine.CalculateAsync(run, period, slips, calculate, cancellationToken))
        lines.Add(new(Guid.Empty, "", "", 0, 0, 0, "", problem));
      audit.Record(AuditActions.Ran, HrmRuns.Arrears, run.Id.Value, period.Label, run.RunLabel ?? EnumText.Label(run.RunType), run.Id.Value,
        detail: $"For {source.Label} · {differences.Count} employee(s) · arrears Rs {differences.Values.Where(d => d.Amount > 0).Sum(d => d.Amount):#,##0}"
          + $" · recoveries Rs {differences.Values.Where(d => d.Amount < 0).Sum(d => d.Amount):#,##0}");
      await context.SaveChangesAsync(cancellationToken);
    }

    return Result<WorkOutArrearsResult>.Success(new(run.Id.Value, source.Label, lines.Count(l => l.Difference != 0),
      lines.Where(l => l.Difference > 0).Sum(l => l.Difference), lines.Where(l => l.Difference < 0).Sum(l => l.Difference), command.DryRun, lines));
  }
}
