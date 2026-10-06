using Microsoft.EntityFrameworkCore;

/// Runs the payroll calculator for a run: for the whole run, or for one employee after an adjustment.
public class PayrollEngine(IApplicationDbContext context, PayrollInputBuilder inputs)
{
  /// A run's slips, tracked, with everything the calculation replaces.
  public Task<List<PayrollTransaction>> SlipsAsync(PayrollRunId runId, CancellationToken cancellationToken, IReadOnlyCollection<EmployeeId>? only = null)
  {
    var slips = context.PayrollTransactions
      .Include(t => t.Segments).Include(t => t.Lines).Include(t => t.LoanDeductions).Include(t => t.Adjustments)
      .AsSplitQuery()
      .Where(t => t.PayrollRunId == runId);
    if (only is not null)
      slips = slips.Where(t => only.Contains(t.EmployeeId));
    return slips.ToListAsync(cancellationToken);
  }

  /// Works out (again) the slips of the employees in the run; new slips are opened, adjustments entered by hand are
  /// kept. Employees that cannot be paid (e.g. no pay record) are left out and reported, one sentence each.
  public async Task<List<string>> CalculateAsync(
      PayrollRun run,
      PayrollPeriod period,
      List<PayrollTransaction> slips,
      IReadOnlyCollection<Employee> employees,
      CancellationToken cancellationToken)
  {
    run.EnsureEditable();
    var problems = new List<string>();
    if (employees.Count == 0)
      return problems;

    var catalog = await inputs.CatalogAsync(period, cancellationToken);
    var taxYear = await inputs.TaxYearAsync(period, cancellationToken);
    if (taxYear is null)
      problems.Add($"No active tax year covers {period.EndDate:yyyy-MM-dd}, so no income tax was withheld. Add the tax year and calculate again.");

    var byEmployee = slips.ToDictionary(t => t.EmployeeId);
    var data = await inputs.InputsAsync(run, period, employees, byEmployee, taxYear, cancellationToken);

    foreach (var employee in employees.OrderBy(e => e.EmployeeNumber))
    {
      try
      {
        var calculation = PayrollCalculator.Calculate(period.Range, run.IsComputed, data[employee.Id], catalog);
        if (!byEmployee.TryGetValue(employee.Id, out var slip))
        {
          slip = PayrollTransaction.Open(PayrollTransactionId.New(), run, employee);
          context.PayrollTransactions.Add(slip);
          byEmployee[employee.Id] = slip;
        }
        slip.ApplyCalculation(calculation);
      }
      catch (DomainException error)
      {
        problems.Add($"{employee.EmployeeNumber}: {DomainMessages.Text(error)}");
        if (byEmployee.TryGetValue(employee.Id, out var stale) && stale.Adjustments.Count == 0)
        {
          context.PayrollTransactions.Remove(stale);
          byEmployee.Remove(employee.Id);
        }
      }
    }

    return problems;
  }
}
