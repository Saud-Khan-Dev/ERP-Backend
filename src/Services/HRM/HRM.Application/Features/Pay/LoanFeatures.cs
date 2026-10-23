using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- loan types ----

public sealed record LoanTypeInput(string Name, Guid? SalaryComponentId, bool IsGpfAdvance, decimal DefaultInterestRate);

public sealed record GetLoanTypesQueryResult(IReadOnlyList<LoanTypeDto> LoanTypes);
public sealed record GetLoanTypesQuery(bool IncludeInactive) : IQuery<Result<GetLoanTypesQueryResult>>;
public sealed record CreateLoanTypeCommand(LoanTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateLoanTypeCommand(Guid Id, LoanTypeInput Type) : ICommand<Result<UpdatedResult>>;
public sealed record SetLoanTypeActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class LoanTypeInputValidator : AbstractValidator<LoanTypeInput>
{
  public LoanTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.DefaultInterestRate).InclusiveBetween(0, 100);
  }
}

public class CreateLoanTypeCommandValidator : AbstractValidator<CreateLoanTypeCommand>
{
  public CreateLoanTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new LoanTypeInputValidator());
}

public class UpdateLoanTypeCommandValidator : AbstractValidator<UpdateLoanTypeCommand>
{
  public UpdateLoanTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new LoanTypeInputValidator());
}

// ---- loans ----

public sealed record GetLoansQueryResult(PaginatedResult<LoanSummaryDto> Loans);
/// Overdue = active loans with an installment due and not recovered.
public sealed record GetLoansQuery(PaginationRequest Pagination, Guid? EmployeeId, Guid? LoanTypeId, LoanStatus? Status, bool Overdue = false) : IQuery<Result<GetLoansQueryResult>>;

public sealed record GetLoanQueryResult(LoanDto Loan);
public sealed record GetLoanQuery(Guid Id, Guid? OnlyForEmployee = null) : IQuery<Result<GetLoanQueryResult>>;

/// Enters a loan or advance application (pending). Interest defaults to the type's annual rate over the term. SanctionNow:
/// the same person sanctions it at once - only when the duty rule LOAN_SANCTION is off and they hold PAYROLL.APPROVE.
public sealed record SanctionLoanCommand(
  Guid EmployeeId,
  Guid LoanTypeId,
  decimal PrincipalAmount,
  decimal? InterestAmount,
  int InstallmentsCount,
  DateOnly StartDate,
  int DeductionPriority,
  bool SanctionNow = false) : ICommand<Result<CreatedResult>>;

/// Sanctions a pending application (someone other than who entered it, while LOAN_SANCTION is on) and lays out its
/// installments. A GP Fund advance is paid out of the employee's GP Fund and cannot exceed its balance.
public sealed record SanctionLoanApplicationCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public sealed record InstallmentPaymentDto(Guid InstallmentId, int InstallmentNumber, decimal Amount);
public sealed record LoanRepaymentResult(decimal Applied, decimal RemainingBalance, LoanStatus Status, IReadOnlyList<InstallmentPaymentDto> Installments);

/// A repayment outside payroll (cash or bank deposit), applied to the oldest open installments and kept with its
/// receipt or challan number and who recorded it.
public sealed record RepayLoanCommand(Guid Id, decimal Amount, DateOnly? PaidOn, string? Remarks, string? ReceiptNumber = null) : ICommand<Result<LoanRepaymentResult>>;

public enum LoanAction
{
  Cancel,
  WriteOff
}

public sealed record CloseLoanCommand(Guid Id, LoanAction Action, string? Remarks) : ICommand<Result<UpdatedResult>>;
public sealed record WaiveInstallmentCommand(Guid Id, Guid InstallmentId, string? Reason = null) : ICommand<Result<UpdatedResult>>;
public sealed record ChangeLoanPriorityCommand(Guid Id, int DeductionPriority) : ICommand<Result<UpdatedResult>>;

public class SanctionLoanCommandValidator : AbstractValidator<SanctionLoanCommand>
{
  public SanctionLoanCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.LoanTypeId).NotEmpty();
    RuleFor(x => x.PrincipalAmount).GreaterThan(0);
    RuleFor(x => x.InterestAmount).GreaterThanOrEqualTo(0).When(x => x.InterestAmount.HasValue);
    RuleFor(x => x.InstallmentsCount).InclusiveBetween(1, 600);
    RuleFor(x => x.DeductionPriority).InclusiveBetween(0, 1000);
  }
}

public class RepayLoanCommandValidator : AbstractValidator<RepayLoanCommand>
{
  public RepayLoanCommandValidator()
  {
    RuleFor(x => x.Amount).GreaterThan(0);
    RuleFor(x => x.Remarks).MaximumLength(2000);
    RuleFor(x => x.ReceiptNumber).NotEmpty().WithMessage("Give the receipt or challan number of the repayment.").MaximumLength(100);
  }
}

public class CloseLoanCommandValidator : AbstractValidator<CloseLoanCommand>
{
  public CloseLoanCommandValidator() => RuleFor(x => x.Remarks).NotEmpty().WithMessage("Give the reason for closing the loan.").MaximumLength(1000);
}

public class WaiveInstallmentCommandValidator : AbstractValidator<WaiveInstallmentCommand>
{
  public WaiveInstallmentCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Give the reason (the sanction) for waiving the installment.").MaximumLength(1000);
}

public class LoanHandlers(IApplicationDbContext context, HrLookup lookup, ICurrentUser currentUser, IClock clock, DutyGuard duty, IAuditTrail audit) :
  IQueryHandler<GetLoanTypesQuery, Result<GetLoanTypesQueryResult>>,
  ICommandHandler<CreateLoanTypeCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateLoanTypeCommand, Result<UpdatedResult>>,
  ICommandHandler<SetLoanTypeActivationCommand, Result<UpdatedResult>>,
  IQueryHandler<GetLoansQuery, Result<GetLoansQueryResult>>,
  IQueryHandler<GetLoanQuery, Result<GetLoanQueryResult>>,
  ICommandHandler<SanctionLoanCommand, Result<CreatedResult>>,
  ICommandHandler<SanctionLoanApplicationCommand, Result<UpdatedResult>>,
  ICommandHandler<RepayLoanCommand, Result<LoanRepaymentResult>>,
  ICommandHandler<CloseLoanCommand, Result<UpdatedResult>>,
  ICommandHandler<WaiveInstallmentCommand, Result<UpdatedResult>>,
  ICommandHandler<ChangeLoanPriorityCommand, Result<UpdatedResult>>
{
  /// Runs that still carry installments they have not recovered yet.
  private static readonly PayrollRunStatus[] Pending = [PayrollRunStatus.Draft, PayrollRunStatus.Calculated, PayrollRunStatus.Reviewed, PayrollRunStatus.Approved];

  // ---- loan types ----

  public async Task<Result<GetLoanTypesQueryResult>> Handle(GetLoanTypesQuery query, CancellationToken cancellationToken)
  {
    var types = await context.LoanTypes.AsNoTracking().Where(t => query.IncludeInactive || t.IsActive).OrderBy(t => t.Name).ToListAsync(cancellationToken);
    var components = await context.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ComponentName, cancellationToken);
    return Result<GetLoanTypesQueryResult>.Success(new(types.Select(t =>
      t.ToDto(t.SalaryComponentId is null ? null : components.GetValueOrDefault(t.SalaryComponentId))).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateLoanTypeCommand command, CancellationToken cancellationToken)
  {
    var i = command.Type;
    var type = LoanType.Create(LoanTypeId.New(), i.Name, await ComponentAsync(i.SalaryComponentId, cancellationToken), i.IsGpfAdvance, i.DefaultInterestRate);
    if (await context.LoanTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A loan type named '{type.Name}' already exists.");

    context.LoanTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateLoanTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await LoadTypeAsync(command.Id, cancellationToken);
    var i = command.Type;
    var used = await context.Loans.AnyAsync(l => l.LoanTypeId == type.Id, cancellationToken);
    if (used && type.IsGpfAdvance != i.IsGpfAdvance)
      return Result<UpdatedResult>.Failure($"'{type.Name}' already has loans; whether it is a GP Fund advance cannot change.");
    if (used && type.SalaryComponentId?.Value != i.SalaryComponentId)
      return Result<UpdatedResult>.Failure($"'{type.Name}' already has loans; their installments stay on the same deduction line.");

    type.Update(i.Name, await ComponentAsync(i.SalaryComponentId, cancellationToken), i.IsGpfAdvance, i.DefaultInterestRate);
    if (await context.LoanTypes.AnyAsync(t => t.Id != type.Id && t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another loan type is named '{type.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetLoanTypeActivationCommand command, CancellationToken cancellationToken)
  {
    var type = await LoadTypeAsync(command.Id, cancellationToken);
    type.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- loans ----

  public async Task<Result<GetLoansQueryResult>> Handle(GetLoansQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Loans.AsNoTracking();
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(l => l.EmployeeId == employeeId);
    }
    if (query.LoanTypeId is { } type)
    {
      var typeId = LoanTypeId.Of(type);
      rows = rows.Where(l => l.LoanTypeId == typeId);
    }
    if (query.Status is { } status)
      rows = rows.Where(l => l.Status == status);
    if (query.Overdue)
    {
      var today = clock.Today;
      rows = rows.Where(l => l.Status == LoanStatus.Active && context.LoanInstallments.Any(i => i.EmployeeLoanId == l.Id && i.DueDate < today
        && (i.Status == InstallmentStatus.Pending || i.Status == InstallmentStatus.Partial)));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(l => l.StartDate).ThenByDescending(l => l.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await SummariesAsync(page, cancellationToken);
    return Result<GetLoansQueryResult>.Success(new(new PaginatedResult<LoanSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetLoanQueryResult>> Handle(GetLoanQuery query, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(query.Id, cancellationToken);
    if (query.OnlyForEmployee is { } own && loan.EmployeeId.Value != own)
      throw new LoanNotFoundException($"Loan {query.Id} was not found.");

    var summary = (await SummariesAsync([loan], cancellationToken))[0];
    var today = clock.Today;
    var installments = loan.Installments;
    var repayments = await context.LoanRepayments.AsNoTracking().Where(r => r.EmployeeLoanId == loan.Id).OrderByDescending(r => r.PaidOn)
      .Select(r => new LoanRepaymentDto(r.Id.Value, r.PaidOn, r.Amount, r.ReceiptNumber, r.Remarks, r.CreatedBy, r.CreatedAt)).ToListAsync(cancellationToken);
    return Result<GetLoanQueryResult>.Success(new(new LoanDto(summary, installments.Sum(i => i.PaidAmount),
      installments.Where(i => i.IsOpen && i.DueDate <= today).Sum(i => i.Outstanding), installments.Select(i => i.ToDto()).ToList(), repayments)));
  }

  public async Task<Result<CreatedResult>> Handle(SanctionLoanCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var type = await LoadTypeAsync(command.LoanTypeId, cancellationToken);
    if (command.SanctionNow)
    {
      if (!currentUser.HasPermission(PermissionCatalog.Payroll.Approve))
        return Result<CreatedResult>.Failure("Sanctioning a loan needs PAYROLL.APPROVE; enter the application and let an approving officer sanction it.");
      await duty.EnsureNotOwnRecordAsync(employee.Id, "sanction a loan", employee, cancellationToken);
      await duty.EnsureSeparateAsync(DutyRules.LoanSanction, currentUser.UserId, "sanction a loan you entered", employee, cancellationToken);
    }

    var loan = EmployeeLoan.Apply(EmployeeLoanId.New(), employee, type, command.PrincipalAmount, command.InterestAmount, command.InstallmentsCount,
      command.StartDate, command.DeductionPriority, clock.Today);
    context.Loans.Add(loan);
    if (command.SanctionNow && await SanctionAsync(loan, employee, type, cancellationToken) is { } problem)
      return Result<CreatedResult>.Failure(problem);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(loan.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(SanctionLoanApplicationCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    if (!loan.IsPending)
      return Result<UpdatedResult>.Failure($"The loan is {EnumText.Words(loan.Status)}; only a pending application can be sanctioned.");
    await duty.EnsureNotOwnRecordAsync(loan.EmployeeId, "sanction a loan", loan, cancellationToken);
    // maker-checker (LOAN_SANCTION): whoever entered the application does not sanction it
    await duty.EnsureSeparateAsync(DutyRules.LoanSanction, loan.CreatedBy, "sanction a loan you entered", loan, cancellationToken);

    var employee = await context.LoadEmployeeAsync(loan.EmployeeId.Value, cancellationToken);
    var type = await LoadTypeAsync(loan.LoanTypeId.Value, cancellationToken);
    if (await SanctionAsync(loan, employee, type, cancellationToken) is { } problem)
      return Result<UpdatedResult>.Failure(problem);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// Lays out the schedule; a GP Fund advance is paid out of the fund now.
  private async Task<string?> SanctionAsync(EmployeeLoan loan, Employee employee, LoanType type, CancellationToken cancellationToken)
  {
    if (type.IsGpfAdvance)
    {
      var account = await context.GpFundAccounts.FirstOrDefaultAsync(a => a.EmployeeId == employee.Id, cancellationToken)
        ?? throw new DomainException($"{employee.DisplayName} has no GP Fund account to advance from.");
      account.EnsureOpen();
      if (await context.Loans.AnyAsync(l => l.Id != loan.Id && l.EmployeeId == employee.Id && l.LoanTypeId == type.Id && l.Status == LoanStatus.Active, cancellationToken))
        return $"{employee.DisplayName} is still repaying a GP Fund advance.";

      loan.Sanction(employee, type, currentUser.UserId, clock.Today);
      var balance = await GpFundBalanceAsync(account.Id, cancellationToken);
      context.GpFundTransactions.Add(GpFundTransaction.Advance(account, clock.Today, loan.PrincipalAmount, loan.Id, balance));
    }
    else
    {
      loan.Sanction(employee, type, currentUser.UserId, clock.Today);
    }

    audit.Record(HrmVerbs.Sanctioned, loan, $"Rs {loan.TotalRepayable:#,##0.##} in {loan.InstallmentsCount} installment(s) of Rs {loan.MonthlyInstallment:#,##0.##} from {loan.StartDate:yyyy-MM-dd}");
    return null;
  }

  public async Task<Result<LoanRepaymentResult>> Handle(RepayLoanCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(loan.EmployeeId, "record a loan repayment", loan, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var paidOn = command.PaidOn ?? clock.Today;
    var record = LoanRepayment.Record(loan, paidOn, command.Amount, command.ReceiptNumber, command.Remarks, clock.Today);

    var applied = loan.Repay(command.Amount);
    var total = applied.Sum(a => a.Amount);
    context.LoanRepayments.Add(record);
    await CreditGpFundAsync(loan, paidOn, total, $"Advance repaid outside payroll{(string.IsNullOrWhiteSpace(command.Remarks) ? "" : $": {command.Remarks.Trim()}")}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<LoanRepaymentResult>.Success(new(total, loan.RemainingBalance, loan.Status,
      applied.Select(a => new InstallmentPaymentDto(a.InstallmentId.Value, a.InstallmentNumber, a.Amount)).ToList()));
  }

  public async Task<Result<UpdatedResult>> Handle(CloseLoanCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(loan.EmployeeId, command.Action == LoanAction.Cancel ? "cancel a loan" : "write a loan off", loan, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var type = await context.LoanTypes.AsNoTracking().FirstAsync(t => t.Id == loan.LoanTypeId, cancellationToken);

    if (command.Action == LoanAction.Cancel)
    {
      var wasPending = loan.IsPending;
      loan.Cancel(command.Remarks, currentUser.UserId, clock.UtcNow);
      audit.Record(wasPending ? HrmVerbs.Rejected : HrmVerbs.Cancelled, loan, $"Reason: {command.Remarks}");
      // nothing was recovered, so a GP Fund advance goes back into the fund in full (a pending one was never paid out)
      if (type.IsGpfAdvance && !wasPending)
        await CreditGpFundAsync(loan, clock.Today, loan.PrincipalAmount, "GP Fund advance cancelled", cancellationToken, adjustment: true);
    }
    else
    {
      if (type.IsGpfAdvance)
        return Result<UpdatedResult>.Failure("A GP Fund advance is the employee's own money; recover it or settle it against the fund, not write it off.");
      loan.WriteOff(command.Remarks, currentUser.UserId, clock.UtcNow);
      audit.Record(HrmVerbs.WrittenOff, loan, $"Reason: {command.Remarks} · Rs {loan.RemainingBalance:#,##0.##} given up");
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(WaiveInstallmentCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(loan.EmployeeId, "waive a loan installment", loan, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var type = await context.LoanTypes.AsNoTracking().FirstAsync(t => t.Id == loan.LoanTypeId, cancellationToken);
    if (type.IsGpfAdvance)
      return Result<UpdatedResult>.Failure("Installments of a GP Fund advance cannot be waived; the fund must be made whole.");

    var installment = loan.Installment(LoanInstallmentId.Of(command.InstallmentId));
    var forgiven = loan.WaiveInstallment(installment.Id, command.Reason, currentUser.UserId, clock.UtcNow);
    audit.Record(HrmVerbs.Waived, loan, $"Installment {installment.InstallmentNumber} (due {installment.DueDate:yyyy-MM-dd}): Rs {forgiven:#,##0.##} forgiven · Reason: {command.Reason}");
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeLoanPriorityCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    loan.ChangePriority(command.DeductionPriority);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<LoanType> LoadTypeAsync(Guid id, CancellationToken cancellationToken)
  {
    var typeId = LoanTypeId.Of(id);
    return await context.LoanTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new LoanTypeNotFoundException($"Loan type {id} was not found.");
  }

  private async Task<SalaryComponent?> ComponentAsync(Guid? id, CancellationToken cancellationToken) =>
      id is { } given ? await context.LoadComponentAsync(given, cancellationToken) : null;

  private async Task<decimal> GpFundBalanceAsync(GpFundAccountId accountId, CancellationToken cancellationToken) =>
      await context.GpFundTransactions.Where(t => t.GpFundAccountId == accountId).SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;

  /// Money paid back on a GP Fund advance returns to the employee's fund.
  private async Task CreditGpFundAsync(EmployeeLoan loan, DateOnly date, decimal amount, string remarks, CancellationToken cancellationToken, bool adjustment = false)
  {
    var type = await context.LoanTypes.AsNoTracking().FirstAsync(t => t.Id == loan.LoanTypeId, cancellationToken);
    if (!type.IsGpfAdvance || amount <= 0)
      return;

    var account = await context.GpFundAccounts.FirstAsync(a => a.EmployeeId == loan.EmployeeId, cancellationToken);
    context.GpFundTransactions.Add(adjustment
      ? GpFundTransaction.Adjustment(account, date, amount, loan.Id, null, remarks)
      : GpFundTransaction.AdvanceRecovery(account, date, amount, loan.Id, null, remarks));
  }

  /// An installment already put on a pay slip that is not finalized yet would be recovered twice.
  private async Task EnsureNotInPayrollAsync(EmployeeLoan loan, CancellationToken cancellationToken)
  {
    var run = await (
        from d in context.PayrollLoanDeductions
        where d.EmployeeLoanId == loan.Id
        join t in context.PayrollTransactions on d.PayrollTransactionId equals t.Id
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        where Pending.Contains(r.Status)
        join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
        select new { p.Year, p.Month, r.Status })
      .FirstOrDefaultAsync(cancellationToken);

    if (run is not null)
      throw new DomainException($"The {run.Year}-{run.Month:00} payroll ({EnumText.Words(run.Status)}) is recovering an installment of this loan; "
        + "finalize it, or recalculate it after this change.");
  }

  private async Task<List<LoanSummaryDto>> SummariesAsync(List<EmployeeLoan> loans, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(loans.Select(l => (EmployeeId?)l.EmployeeId), cancellationToken);
    var types = await context.LoanTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
    return loans.Select(l =>
    {
      var person = people.GetValueOrDefault(l.EmployeeId.Value);
      var type = types.GetValueOrDefault(l.LoanTypeId);
      return new LoanSummaryDto(l.Id.Value, l.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", l.LoanTypeId.Value,
        type?.Name, l.PrincipalAmount, l.InterestAmount, l.InstallmentsCount, l.MonthlyInstallment, l.StartDate, l.EndDate,
        l.RemainingBalance, l.DeductionPriority, l.Status, l.ApprovedBy, l.CreatedAt, type?.IsGpfAdvance ?? false, l.ClosureReason, l.ClosedBy, l.ClosedAt, l.CreatedBy);
    }).ToList();
  }
}
