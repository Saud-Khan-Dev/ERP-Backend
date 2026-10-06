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

public sealed record BankAdviceLine(string EmployeeNumber, string EmployeeName, string? AccountNumber, string? Iban, string? BranchName, decimal Amount, PaymentStatus Status);
public sealed record BankAdviceBank(string BankName, int Count, decimal Amount, IReadOnlyList<BankAdviceLine> Lines);
public sealed record BankAdviceResult(Guid RunId, string Period, PayrollRunType RunType, int Count, decimal Amount, IReadOnlyList<BankAdviceBank> Banks);

/// The letter to the bank: the run's pending and processed payments, bank by bank.
public sealed record GetBankAdviceQuery(Guid RunId) : IQuery<Result<BankAdviceResult>>;

public class GeneratePaymentsCommandValidator : AbstractValidator<GeneratePaymentsCommand>
{
  public GeneratePaymentsCommandValidator() => RuleFor(x => x.PaymentMethod).IsInEnum();
}

public class ProcessPaymentsCommandValidator : AbstractValidator<ProcessPaymentsCommand>
{
  public ProcessPaymentsCommandValidator() => RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
}

public class PayrollPaymentHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  ICommandHandler<GeneratePaymentsCommand, Result<GeneratePaymentsResult>>,
  IQueryHandler<GetPaymentsQuery, Result<GetPaymentsQueryResult>>,
  ICommandHandler<ProcessPaymentsCommand, Result<ProcessPaymentsResult>>,
  ICommandHandler<ChangePaymentStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<GetBankAdviceQuery, Result<BankAdviceResult>>
{
  public async Task<Result<GeneratePaymentsResult>> Handle(GeneratePaymentsCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.RunId, cancellationToken);
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
      if (!accounts.TryGetValue(slip.EmployeeId, out var account))
      {
        problems.Add($"{number}: no active salary bank account.");
        continue;
      }

      var payment = PayrollPayment.Create(PayrollPaymentId.New(), run, slip, account, command.PaymentMethod);
      context.Payments.Add(payment);
      created.Add(payment);
    }

    await context.SaveChangesAsync(cancellationToken);
    var amount = slips.Where(s => created.Any(p => p.PayrollTransactionId == s.Id)).Sum(s => s.NetPayable);
    return Result<GeneratePaymentsResult>.Success(new(created.Count, amount, problems));
  }

  public async Task<Result<GetPaymentsQueryResult>> Handle(GetPaymentsQuery query, CancellationToken cancellationToken)
  {
    var rows = await PaymentsOfAsync(PayrollRunId.Of(query.RunId), query.Status is { } status ? [status] : null, cancellationToken);
    return Result<GetPaymentsQueryResult>.Success(new(rows));
  }

  public async Task<Result<ProcessPaymentsResult>> Handle(ProcessPaymentsCommand command, CancellationToken cancellationToken)
  {
    var runId = PayrollRunId.Of(command.RunId);
    if (command.PaymentDate > clock.Today)
      return Result<ProcessPaymentsResult>.Failure("A payment cannot be dated in the future.");

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

    foreach (var payment in list)
      payment.Process(command.PaymentDate, command.PaymentReference);
    await context.SaveChangesAsync(cancellationToken);

    var slipIds = list.Select(p => p.PayrollTransactionId).ToList();
    var amount = await context.PayrollTransactions.Where(t => slipIds.Contains(t.Id)).SumAsync(t => t.NetPayable, cancellationToken);
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

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<BankAdviceResult>> Handle(GetBankAdviceQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(query.RunId, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    var payments = await PaymentsOfAsync(run.Id, [PaymentStatus.Pending, PaymentStatus.Processed], cancellationToken);

    var banks = payments.GroupBy(p => p.BankName ?? "(no bank)").OrderBy(g => g.Key).Select(g => new BankAdviceBank(g.Key, g.Count(), g.Sum(p => p.Amount),
      g.OrderBy(p => p.EmployeeNumber).Select(p => new BankAdviceLine(p.EmployeeNumber, p.EmployeeName, p.AccountNumber, p.Iban, p.BranchName, p.Amount, p.PaymentStatus)).ToList()))
      .ToList();
    return Result<BankAdviceResult>.Success(new(run.Id.Value, period.Label, run.RunType, payments.Count, payments.Sum(p => p.Amount), banks));
  }

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
    return rows.Select(r =>
    {
      var p = r.Payment;
      var person = people.GetValueOrDefault(p.EmployeeId.Value);
      return new PaymentDto(p.Id.Value, p.PayrollTransactionId.Value, p.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", r.NetPayable,
        p.BankNameSnapshot, p.BranchNameSnapshot, p.AccountNumberSnapshot, p.IbanSnapshot, p.PaymentMethod, p.PaymentStatus, p.PaymentDate, p.PaymentReference, p.CreatedAt);
    }).OrderBy(p => p.EmployeeNumber).ThenByDescending(p => p.CreatedAt).ToList();
  }
}
