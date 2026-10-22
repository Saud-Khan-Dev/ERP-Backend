using Microsoft.EntityFrameworkCore;

/// One employee's History: the audit trail's lines about the employee and everything that hangs off them. Restricted:
/// the lines whose values were withheld (personal and pay facts, without HR_SENSITIVE.VIEW) - their old / new values
/// and detail are null and the screen shows "Restricted".
public sealed record EmployeeHistoryResult(AuditPageDto Audit, IReadOnlyList<Guid> Restricted);

public sealed record GetEmployeeHistoryQuery(Guid EmployeeId, AuditFilter Filter) : IQuery<Result<EmployeeHistoryResult>>;

public class EmployeeHistoryHandler(IApplicationDbContext context, Sensitive sensitive) :
  IQueryHandler<GetEmployeeHistoryQuery, Result<EmployeeHistoryResult>>
{
  /// Kinds of record whose values are personal or pay facts.
  private static readonly HashSet<string> SensitiveKinds =
  [
    "Contact", "Address", "Emergency contact", "Family member", "Bank account", "Pay record", "Pay slip", "Pay slip adjustment",
    "Salary payment", "Salary override", "Tax opening", "Tax exemption"
  ];

  /// Facts of the employee record itself that are personal or pay facts.
  private static readonly HashSet<string> SensitiveFields = [EmployeeFields.Cnic, EmployeeFields.DateOfBirth, EmployeeFields.Pay];

  public async Task<Result<EmployeeHistoryResult>> Handle(GetEmployeeHistoryQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    // the records that hang off the employee's own records (a loan's repayments, a slip's adjustments, a review's goals ...)
    var ids = new List<Guid> { query.EmployeeId };
    ids.AddRange(await context.Loans.Where(l => l.EmployeeId == employeeId).Select(l => l.Id.Value).ToListAsync(cancellationToken));
    ids.AddRange(await context.GpFundAccounts.Where(a => a.EmployeeId == employeeId).Select(a => a.Id.Value).ToListAsync(cancellationToken));
    ids.AddRange(await context.PayrollTransactions.Where(t => t.EmployeeId == employeeId).Select(t => t.Id.Value).ToListAsync(cancellationToken));
    ids.AddRange(await context.PerformanceReviews.Where(r => r.EmployeeId == employeeId).Select(r => r.Id.Value).ToListAsync(cancellationToken));
    ids.AddRange(await context.Tasks.Where(t => t.EmployeeId == employeeId).Select(t => t.Id.Value).ToListAsync(cancellationToken));

    var trail = context.AuditEntries.Where(x => x.RecordId == query.EmployeeId || (x.ParentId != null && ids.Contains(x.ParentId.Value)));
    var page = await AuditQueries.ListAsync(trail, query.Filter with { ParentId = null, RecordId = null }, cancellationToken);

    if (sensitive.CanView)
      return Result<EmployeeHistoryResult>.Success(new(page, []));

    var restricted = new List<Guid>();
    var lines = page.Data.Select(line =>
    {
      var personal = SensitiveKinds.Contains(line.RecordKind)
        || (line.RecordKind == "Employee" && (line.Action == AuditActions.Added || (line.Field is { } f && SensitiveFields.Contains(f))));
      if (!personal)
        return line;
      restricted.Add(line.Id);
      return line with { OldValue = null, NewValue = null, Detail = null };
    }).ToList();
    return Result<EmployeeHistoryResult>.Success(new(new AuditPageDto(lines, page.Count), restricted));
  }
}
