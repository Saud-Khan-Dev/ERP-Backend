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
public sealed record GetLoansQuery(PaginationRequest Pagination, Guid? EmployeeId, Guid? LoanTypeId, LoanStatus? Status) : IQuery<Result<GetLoansQueryResult>>;

public sealed record GetLoanQueryResult(LoanDto Loan);
public sealed record GetLoanQuery(Guid Id, Guid? OnlyForEmployee = null) : IQuery<Result<GetLoanQueryResult>>;

/// Sanctions a loan or advance and lays out its installments. Interest defaults to the type's annual rate over the term.
/// A GP Fund advance is paid out of the employee's GP Fund and cannot exceed its balance.
public sealed record SanctionLoanCommand(
  Guid EmployeeId,
  Guid LoanTypeId,
  decimal PrincipalAmount,
  decimal? InterestAmount,
  int InstallmentsCount,
  DateOnly StartDate,
  int DeductionPriority) : ICommand<Result<CreatedResult>>;

public sealed record InstallmentPaymentDto(Guid InstallmentId, int InstallmentNumber, decimal Amount);
public sealed record LoanRepaymentResult(decimal Applied, decimal RemainingBalance, LoanStatus Status, IReadOnlyList<InstallmentPaymentDto> Installments);

/// A repayment outside payroll (cash or bank deposit), applied to the oldest open installments.
public sealed record RepayLoanCommand(Guid Id, decimal Amount, DateOnly? PaidOn, string? Remarks) : ICommand<Result<LoanRepaymentResult>>;

public enum LoanAction
{
  Cancel,
  WriteOff
}

public sealed record CloseLoanCommand(Guid Id, LoanAction Action, string? Remarks) : ICommand<Result<UpdatedResult>>;
public sealed record WaiveInstallmentCommand(Guid Id, Guid InstallmentId) : ICommand<Result<UpdatedResult>>;
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
  }
}

public class LoanHandlers(IApplicationDbContext context, HrLookup lookup, ICurrentUser currentUser, IClock clock) :
  IQueryHandler<GetLoanTypesQuery, Result<GetLoanTypesQueryResult>>,
  ICommandHandler<CreateLoanTypeCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateLoanTypeCommand, Result<UpdatedResult>>,
  ICommandHandler<SetLoanTypeActivationCommand, Result<UpdatedResult>>,
  IQueryHandler<GetLoansQuery, Result<GetLoansQueryResult>>,
  IQueryHandler<GetLoanQuery, Result<GetLoanQueryResult>>,
  ICommandHandler<SanctionLoanCommand, Result<CreatedResult>>,
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
    return Result<GetLoanQueryResult>.Success(new(new LoanDto(summary, installments.Sum(i => i.PaidAmount),
      installments.Where(i => i.IsOpen && i.DueDate <= today).Sum(i => i.Outstanding), installments.Select(i => i.ToDto()).ToList())));
  }

  public async Task<Result<CreatedResult>> Handle(SanctionLoanCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var type = await LoadTypeAsync(command.LoanTypeId, cancellationToken);
    var loan = EmployeeLoan.Sanction(EmployeeLoanId.New(), employee, type, command.PrincipalAmount, command.InterestAmount, command.InstallmentsCount,
      command.StartDate, command.DeductionPriority, currentUser.UserId);

    if (type.IsGpfAdvance)
    {
      var account = await context.GpFundAccounts.FirstOrDefaultAsync(a => a.EmployeeId == employee.Id, cancellationToken)
        ?? throw new DomainException($"{employee.DisplayName} has no GP Fund account to advance from.");
      account.EnsureOpen();
      if (await context.Loans.AnyAsync(l => l.EmployeeId == employee.Id && l.LoanTypeId == type.Id && l.Status == LoanStatus.Active, cancellationToken))
        return Result<CreatedResult>.Failure($"{employee.DisplayName} is still repaying a GP Fund advance.");

      var balance = await GpFundBalanceAsync(account.Id, cancellationToken);
      context.GpFundTransactions.Add(GpFundTransaction.Advance(account, clock.Today, loan.PrincipalAmount, loan.Id, balance));
    }

    context.Loans.Add(loan);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(loan.Id);
  }

  public async Task<Result<LoanRepaymentResult>> Handle(RepayLoanCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var paidOn = command.PaidOn ?? clock.Today;
    if (paidOn > clock.Today)
      return Result<LoanRepaymentResult>.Failure("A repayment cannot be dated in the future.");

    var applied = loan.Repay(command.Amount);
    var total = applied.Sum(a => a.Amount);
    await CreditGpFundAsync(loan, paidOn, total, $"Advance repaid outside payroll{(string.IsNullOrWhiteSpace(command.Remarks) ? "" : $": {command.Remarks.Trim()}")}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<LoanRepaymentResult>.Success(new(total, loan.RemainingBalance, loan.Status,
      applied.Select(a => new InstallmentPaymentDto(a.InstallmentId.Value, a.InstallmentNumber, a.Amount)).ToList()));
  }

  public async Task<Result<UpdatedResult>> Handle(CloseLoanCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var type = await context.LoanTypes.AsNoTracking().FirstAsync(t => t.Id == loan.LoanTypeId, cancellationToken);

    if (command.Action == LoanAction.Cancel)
    {
      loan.Cancel();
      // nothing was recovered, so a GP Fund advance goes back into the fund in full
      if (type.IsGpfAdvance)
        await CreditGpFundAsync(loan, clock.Today, loan.PrincipalAmount, "GP Fund advance cancelled", cancellationToken, adjustment: true);
    }
    else
    {
      if (type.IsGpfAdvance)
        return Result<UpdatedResult>.Failure("A GP Fund advance is the employee's own money; recover it or settle it against the fund, not write it off.");
      loan.WriteOff();
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(WaiveInstallmentCommand command, CancellationToken cancellationToken)
  {
    var loan = await context.LoadLoanAsync(command.Id, cancellationToken);
    await EnsureNotInPayrollAsync(loan, cancellationToken);
    var type = await context.LoanTypes.AsNoTracking().FirstAsync(t => t.Id == loan.LoanTypeId, cancellationToken);
    if (type.IsGpfAdvance)
      return Result<UpdatedResult>.Failure("Installments of a GP Fund advance cannot be waived; the fund must be made whole.");

    loan.WaiveInstallment(LoanInstallmentId.Of(command.InstallmentId));
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
    var types = await context.LoanTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    return loans.Select(l =>
    {
      var person = people.GetValueOrDefault(l.EmployeeId.Value);
      return new LoanSummaryDto(l.Id.Value, l.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", l.LoanTypeId.Value,
        types.GetValueOrDefault(l.LoanTypeId), l.PrincipalAmount, l.InterestAmount, l.InstallmentsCount, l.MonthlyInstallment, l.StartDate, l.EndDate,
        l.RemainingBalance, l.DeductionPriority, l.Status, l.ApprovedBy, l.CreatedAt);
    }).ToList();
  }
}
