using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GeneratePaymentsResult(int Created, decimal Amount, IReadOnlyList<string> Problems);

/// Makes a pending payment for every pay slip of a finalized run that is not held, has something to pay and has no
/// live payment yet, into the employee's salary (primary) account. Run it again after fixing problems or after a
/// payment failed or came back.
public sealed record GeneratePaymentsCommand(Guid RunId, PaymentMethod PaymentMethod) : ICommand<Result<GeneratePaymentsResult>>;

public sealed record GetPaymentsQueryResult(IReadOnlyList<PaymentDto> Payments);
public sealed record GetPaymentsQuery(Guid RunId, PaymentStatus? Status) : IQuery<Result<GetPaymentsQueryResult>>;

public sealed record ProcessPaymentsResult(int Processed, decimal Amount);

/// The bank confirmed the transfer: pending payments of the run (or the ones given) are marked processed.
public sealed record ProcessPaymentsCommand(Guid RunId, DateOnly PaymentDate, string PaymentReference, IReadOnlyList<Guid>? PaymentIds) : ICommand<Result<ProcessPaymentsResult>>;

public enum PaymentAction
{
  Fail,
  Return
}

/// Fail: the bank refused a pending payment. Return: a processed payment came back.
public sealed record ChangePaymentStatusCommand(Guid Id, PaymentAction Action, string? PaymentReference) : ICommand<Result<UpdatedResult>>;

public sealed record BankAdviceLine(string EmployeeNumber, string EmployeeName, string? AccountNumber, string? Iban, string? BranchName, decimal? Amount, PaymentStatus Status);
public sealed record BankAdviceBank(string BankName, int Count, decimal Amount, IReadOnlyList<BankAdviceLine> Lines);
/// Restricted: what each line withholds from this caller (no HR_SENSITIVE.VIEW): "pay", "accountNumber", "iban"; the bank totals stay.
public sealed record BankAdviceResult(Guid RunId, string Period, PayrollRunType RunType, int Count, decimal Amount, IReadOnlyList<BankAdviceBank> Banks,
  IReadOnlyList<string>? Restricted = null);

/// The letter to the bank: the run's pending and processed bank transfers, bank by bank (cheques and cash are not in it).
public sealed record GetBankAdviceQuery(Guid RunId) : IQuery<Result<BankAdviceResult>>;

/// Where the run's net pay goes: net payable = to the banks + by cheque + in cash + held + not paid yet (no payment made,
/// usually no salary account) - each with its count. Processed / returned / failed show what the bank has done.
public sealed record PaymentReconciliationResult(
  Guid RunId,
  decimal NetPayable,
  int Slips,
  decimal ToBanks, int ToBanksCount,
  decimal ByCheque, int ByChequeCount,
  decimal InCash, int InCashCount,
  decimal Held, int HeldCount,
  decimal NotPaid, int NotPaidCount,
  decimal Processed, int ProcessedCount,
  int FailedOrReturned,
  int WithoutAccount);

public sealed record GetPaymentReconciliationQuery(Guid RunId) : IQuery<Result<PaymentReconciliationResult>>;

public class GeneratePaymentsCommandValidator : AbstractValidator<GeneratePaymentsCommand>
{
  public GeneratePaymentsCommandValidator() => RuleFor(x => x.PaymentMethod).IsInEnum();
}

public class ProcessPaymentsCommandValidator : AbstractValidator<ProcessPaymentsCommand>
{
  public ProcessPaymentsCommandValidator() => RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
}

public class PayrollPaymentHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock, OfficeScope scope, DutyGuard duty, Sensitive sensitive, IAuditTrail audit) :
  ICommandHandler<GeneratePaymentsCommand, Result<GeneratePaymentsResult>>,
  IQueryHandler<GetPaymentsQuery, Result<GetPaymentsQueryResult>>,
  ICommandHandler<ProcessPaymentsCommand, Result<ProcessPaymentsResult>>,
  ICommandHandler<ChangePaymentStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<GetBankAdviceQuery, Result<BankAdviceResult>>,
  IQueryHandler<GetPaymentReconciliationQuery, Result<PaymentReconciliationResult>>
{
  public async Task<Result<GeneratePaymentsResult>> Handle(GeneratePaymentsCommand command, CancellationToken cancellationToken)
  {
    if (await scope.WholeOrganizationRefusalAsync("prepare a payroll run's salary payments", cancellationToken) is { } refusal)
      return Result<GeneratePaymentsResult>.Failure(refusal);
    var run = await context.LoadPayrollRunAsync(command.RunId, cancellationToken);
    await duty.EnsureSeparateAsync(DutyRules.PayrollPay, run.ApprovedBy, "pay out a payroll run you approved", run, cancellationToken);
    if (run.Status is not (PayrollRunStatus.Finalized or PayrollRunStatus.Paid))
      return Result<GeneratePaymentsResult>.Failure($"The run is {EnumText.Words(run.Status)}; salaries are paid only from a finalized run.");

    var slips = await context.PayrollTransactions.AsNoTracking()
      .Where(t => t.PayrollRunId == run.Id && t.Status != PayrollTransactionStatus.Held && t.NetPayable > 0
        && !context.Payments.Any(p => p.PayrollTransactionId == t.Id && (p.PaymentStatus == PaymentStatus.Pending || p.PaymentStatus == PaymentStatus.Processed)))
      .ToListAsync(cancellationToken);
    var employeeIds = slips.Select(s => s.EmployeeId).ToList();
    var accounts = await context.BankAccounts.AsNoTracking()
      .Where(a => employeeIds.Contains(a.EmployeeId) && a.IsPrimary && a.Status == RecordStatus.Active)
      .ToDictionaryAsync(a => a.EmployeeId, cancellationToken);
    var people = await lookup.EmployeesAsync(employeeIds.Select(e => (EmployeeId?)e), cancellationToken);

    var problems = new List<string>();
    var created = new List<PayrollPayment>();
    foreach (var slip in slips.OrderBy(s => people.GetValueOrDefault(s.EmployeeId.Value)?.EmployeeNumber))
    {
      var number = people.GetValueOrDefault(slip.EmployeeId.Value)?.EmployeeNumber ?? slip.EmployeeId.Value.ToString();
      accounts.TryGetValue(slip.EmployeeId, out var account);
      if (account is null && command.PaymentMethod == PaymentMethod.BankTransfer)
      {
        problems.Add($"{number}: no active salary bank account - add one, or pay by cheque or cash.");
        continue;
      }

      var payment = PayrollPayment.Create(PayrollPaymentId.New(), run, slip, account, command.PaymentMethod);
      context.Payments.Add(payment);
      created.Add(payment);
    }

    var amount = slips.Where(s => created.Any(p => p.PayrollTransactionId == s.Id)).Sum(s => s.NetPayable);
    using (audit.Quietly())
    {
      audit.Record(AuditActions.Ran, HrmRuns.SalaryPayments, run.Id.Value, null, run.RunLabel ?? EnumText.Label(run.RunType), run.Id.Value,
        detail: $"Prepared {created.Count} payment(s) by {EnumText.Words(command.PaymentMethod)} · Rs {amount:#,##0} · {problems.Count} without an account");
      await context.SaveChangesAsync(cancellationToken);
    }
    return Result<GeneratePaymentsResult>.Success(new(created.Count, amount, problems));
  }

  public async Task<Result<GetPaymentsQueryResult>> Handle(GetPaymentsQuery query, CancellationToken cancellationToken)
  {
    var rows = await PaymentsOfAsync(PayrollRunId.Of(query.RunId), query.Status is { } status ? [status] : null, cancellationToken);
    return Result<GetPaymentsQueryResult>.Success(new(sensitive.CanView ? rows : rows.Select(Withheld).ToList()));
  }

  public async Task<Result<ProcessPaymentsResult>> Handle(ProcessPaymentsCommand command, CancellationToken cancellationToken)
  {
    var runId = PayrollRunId.Of(command.RunId);
    if (command.PaymentDate > clock.Today)
      return Result<ProcessPaymentsResult>.Failure("A payment cannot be dated in the future.");
    var run = await context.LoadPayrollRunAsync(command.RunId, cancellationToken);
    await duty.EnsureSeparateAsync(DutyRules.PayrollPay, run.ApprovedBy, "record payments of a payroll run you approved", run, cancellationToken);

    var payments = context.Payments.Where(p => p.PaymentStatus == PaymentStatus.Pending
      && context.PayrollTransactions.Any(t => t.Id == p.PayrollTransactionId && t.PayrollRunId == runId));
    if (command.PaymentIds is { Count: > 0 })
    {
      var ids = command.PaymentIds.Select(PayrollPaymentId.Of).ToList();
      payments = payments.Where(p => ids.Contains(p.Id));
    }

    var list = await payments.ToListAsync(cancellationToken);
    if (list.Count == 0)
      return Result<ProcessPaymentsResult>.Failure("There are no pending payments to mark processed.");

    var slipIds = list.Select(p => p.PayrollTransactionId).ToList();
    var amount = await context.PayrollTransactions.Where(t => slipIds.Contains(t.Id)).SumAsync(t => t.NetPayable, cancellationToken);
    using (audit.Quietly())
    {
      foreach (var payment in list)
        payment.Process(command.PaymentDate, command.PaymentReference);
      audit.Record(AuditActions.Ran, HrmRuns.SalaryPayments, run.Id.Value, command.PaymentReference, run.RunLabel ?? EnumText.Label(run.RunType), run.Id.Value,
        detail: $"Marked {list.Count} payment(s) processed on {command.PaymentDate:yyyy-MM-dd}, bank reference {command.PaymentReference} · Rs {amount:#,##0}");
      await context.SaveChangesAsync(cancellationToken);
    }
    return Result<ProcessPaymentsResult>.Success(new(list.Count, amount));
  }

  public async Task<Result<UpdatedResult>> Handle(ChangePaymentStatusCommand command, CancellationToken cancellationToken)
  {
    var paymentId = PayrollPaymentId.Of(command.Id);
    var payment = await context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken)
      ?? throw new PaymentNotFoundException($"Payment {command.Id} was not found.");

    if (command.Action == PaymentAction.Fail)
      payment.Fail(command.PaymentReference);
    else
      payment.MarkReturned(command.PaymentReference);

    audit.Record(command.Action == PaymentAction.Fail ? HrmVerbs.Failed : HrmVerbs.Returned, payment, command.PaymentReference);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<BankAdviceResult>> Handle(GetBankAdviceQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(query.RunId, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    var payments = (await PaymentsOfAsync(run.Id, [PaymentStatus.Pending, PaymentStatus.Processed], cancellationToken))
      .Where(p => p.PaymentMethod == PaymentMethod.BankTransfer).ToList();

    var shows = sensitive.CanView;
    var banks = payments.GroupBy(p => p.BankName ?? "(no bank)").OrderBy(g => g.Key).Select(g => new BankAdviceBank(g.Key, g.Count(), g.Sum(p => p.Amount ?? 0),
      g.OrderBy(p => p.EmployeeNumber).Select(p => shows
        ? new BankAdviceLine(p.EmployeeNumber, p.EmployeeName, p.AccountNumber, p.Iban, p.BranchName, p.Amount, p.PaymentStatus)
        : new BankAdviceLine(p.EmployeeNumber, p.EmployeeName, null, null, p.BranchName, null, p.PaymentStatus)).ToList()))
      .ToList();
    return Result<BankAdviceResult>.Success(new(run.Id.Value, period.Label, run.RunType, payments.Count, payments.Sum(p => p.Amount ?? 0), banks,
      shows ? null : [Sensitive.Pay, Sensitive.AccountNumber, Sensitive.Iban]));
  }

  public async Task<Result<PaymentReconciliationResult>> Handle(GetPaymentReconciliationQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(query.RunId, cancellationToken);
    var slips = await context.PayrollTransactions.AsNoTracking().Where(t => t.PayrollRunId == run.Id)
      .Select(t => new { t.Id, t.EmployeeId, t.NetPayable, Held = t.Status == PayrollTransactionStatus.Held }).ToListAsync(cancellationToken);
    var slipIds = slips.Select(s => s.Id).ToList();
    var payments = await context.Payments.AsNoTracking().Where(p => slipIds.Contains(p.PayrollTransactionId)).ToListAsync(cancellationToken);
    var live = payments.Where(p => p.IsLive).GroupBy(p => p.PayrollTransactionId).ToDictionary(g => g.Key, g => g.First());
    var employees = slips.Select(s => s.EmployeeId).ToList();
    var withAccount = (await context.BankAccounts.AsNoTracking().Where(a => employees.Contains(a.EmployeeId) && a.IsPrimary && a.Status == RecordStatus.Active)
      .Select(a => a.EmployeeId).ToListAsync(cancellationToken)).ToHashSet();

    decimal Sum(Func<PayrollPayment, bool> which) => slips.Where(s => live.TryGetValue(s.Id, out var p) && which(p)).Sum(s => s.NetPayable);
    int Count(Func<PayrollPayment, bool> which) => slips.Count(s => live.TryGetValue(s.Id, out var p) && which(p));
    var held = slips.Where(s => s.Held).ToList();
    var unpaid = slips.Where(s => !s.Held && s.NetPayable > 0 && !live.ContainsKey(s.Id)).ToList();

    return Result<PaymentReconciliationResult>.Success(new(run.Id.Value, slips.Sum(s => s.NetPayable), slips.Count,
      Sum(p => p.PaymentMethod == PaymentMethod.BankTransfer), Count(p => p.PaymentMethod == PaymentMethod.BankTransfer),
      Sum(p => p.PaymentMethod == PaymentMethod.Cheque), Count(p => p.PaymentMethod == PaymentMethod.Cheque),
      Sum(p => p.PaymentMethod == PaymentMethod.Cash), Count(p => p.PaymentMethod == PaymentMethod.Cash),
      held.Sum(s => s.NetPayable), held.Count, unpaid.Sum(s => s.NetPayable), unpaid.Count,
      Sum(p => p.PaymentStatus == PaymentStatus.Processed), Count(p => p.PaymentStatus == PaymentStatus.Processed),
      payments.Count(p => p.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Returned),
      unpaid.Count(s => !withAccount.Contains(s.EmployeeId))));
  }

  /// A payment as someone who may not see pay and bank accounts reads it.
  private static PaymentDto Withheld(PaymentDto p) =>
      p with { Amount = null, AccountNumber = null, Iban = null, Restricted = [Sensitive.Pay, Sensitive.AccountNumber, Sensitive.Iban] };

  private async Task<List<PaymentDto>> PaymentsOfAsync(PayrollRunId runId, IReadOnlyCollection<PaymentStatus>? statuses, CancellationToken cancellationToken)
  {
    var rows = await (
        from p in context.Payments.AsNoTracking()
        join t in context.PayrollTransactions on p.PayrollTransactionId equals t.Id
        where t.PayrollRunId == runId
        select new { Payment = p, t.NetPayable })
      .ToListAsync(cancellationToken);
    if (statuses is not null)
      rows = rows.Where(r => statuses.Contains(r.Payment.PaymentStatus)).ToList();

    var people = await lookup.EmployeesAsync(rows.Select(r => (EmployeeId?)r.Payment.EmployeeId), cancellationToken);
    var list = rows.Select(r =>
    {
      var p = r.Payment;
      var person = people.GetValueOrDefault(p.EmployeeId.Value);
      return new PaymentDto(p.Id.Value, p.PayrollTransactionId.Value, p.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", r.NetPayable,
        p.BankNameSnapshot, p.BranchNameSnapshot, p.AccountNumberSnapshot, p.IbanSnapshot, p.PaymentMethod, p.PaymentStatus, p.PaymentDate, p.PaymentReference,
        p.CreatedAt);
    }).OrderBy(p => p.EmployeeNumber).ThenByDescending(p => p.CreatedAt).ToList();
    return list;
  }
}
