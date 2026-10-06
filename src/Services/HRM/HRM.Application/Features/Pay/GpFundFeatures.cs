using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- accounts and ledger ----

public sealed record GetGpFundAccountsQueryResult(PaginatedResult<GpFundAccountDto> Accounts);
public sealed record GetGpFundAccountsQuery(PaginationRequest Pagination, Guid? EmployeeId, RecordStatus? Status) : IQuery<Result<GetGpFundAccountsQueryResult>>;

public sealed record GetGpFundAccountQueryResult(GpFundAccountDto Account, IReadOnlyList<GpFundTransactionDto> Transactions);

/// One account with its ledger, newest first (by account id, or the employee's account).
public sealed record GetGpFundAccountQuery(Guid? Id, Guid? EmployeeId) : IQuery<Result<GetGpFundAccountQueryResult>>;

/// Opens the employee's GP Fund account; an opening balance (brought from the old register) is its first ledger row.
public sealed record OpenGpFundAccountCommand(Guid EmployeeId, string? AccountNumber, DateOnly OpenedOn, decimal MonthlySubscription, decimal? OpeningBalance)
  : ICommand<Result<CreatedResult>>;

public sealed record UpdateGpFundAccountCommand(Guid Id, string? AccountNumber, decimal MonthlySubscription) : ICommand<Result<UpdatedResult>>;

/// A hand-made ledger row: opening, interest, withdrawal (non-refundable), final payment (the whole balance) or a
/// signed adjustment. Subscriptions and advance recoveries come from payroll; advances from loans.
public sealed record PostGpFundTransactionCommand(Guid AccountId, GpFundTransactionType TransactionType, DateOnly TransactionDate, decimal? Amount, string? Remarks)
  : ICommand<Result<CreatedResult>>;

/// Closes a settled account (balance zero, after the final payment).
public sealed record CloseGpFundAccountCommand(Guid Id, DateOnly ClosedOn) : ICommand<Result<UpdatedResult>>;

public class OpenGpFundAccountCommandValidator : AbstractValidator<OpenGpFundAccountCommand>
{
  public OpenGpFundAccountCommandValidator()
  {
    RuleFor(x => x.AccountNumber).MaximumLength(50);
    RuleFor(x => x.MonthlySubscription).GreaterThanOrEqualTo(0);
    RuleFor(x => x.OpeningBalance).GreaterThanOrEqualTo(0).When(x => x.OpeningBalance.HasValue);
  }
}

public class UpdateGpFundAccountCommandValidator : AbstractValidator<UpdateGpFundAccountCommand>
{
  public UpdateGpFundAccountCommandValidator()
  {
    RuleFor(x => x.AccountNumber).MaximumLength(50);
    RuleFor(x => x.MonthlySubscription).GreaterThanOrEqualTo(0);
  }
}

public class PostGpFundTransactionCommandValidator : AbstractValidator<PostGpFundTransactionCommand>
{
  public PostGpFundTransactionCommandValidator()
  {
    RuleFor(x => x.TransactionType)
      .Must(t => t is GpFundTransactionType.Opening or GpFundTransactionType.Interest or GpFundTransactionType.Withdrawal
        or GpFundTransactionType.FinalPayment or GpFundTransactionType.Adjustment)
      .WithMessage("Post an opening, interest, withdrawal, final payment or adjustment; subscriptions and advance recoveries come from payroll, advances from loans.");
    RuleFor(x => x.Amount).NotNull().NotEqual(0).When(x => x.TransactionType != GpFundTransactionType.FinalPayment)
      .WithMessage("Enter the amount.");
    RuleFor(x => x.Amount).GreaterThan(0).When(x => x.TransactionType is GpFundTransactionType.Opening or GpFundTransactionType.Interest or GpFundTransactionType.Withdrawal)
      .WithMessage("The amount must be above zero.");
    RuleFor(x => x.Remarks).NotEmpty().When(x => x.TransactionType == GpFundTransactionType.Adjustment).WithMessage("Give the reason for the adjustment.");
    RuleFor(x => x.Remarks).MaximumLength(2000);
  }
}

// ---- interest ----

public sealed record GpFundInterestRateInput(string FiscalYear, decimal RatePercent, string? NotificationRef, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record GetGpFundInterestRatesQueryResult(IReadOnlyList<GpFundInterestRateDto> Rates);
public sealed record GetGpFundInterestRatesQuery : IQuery<Result<GetGpFundInterestRatesQueryResult>>;
public sealed record CreateGpFundInterestRateCommand(GpFundInterestRateInput Rate) : ICommand<Result<CreatedResult>>;
public sealed record UpdateGpFundInterestRateCommand(Guid Id, GpFundInterestRateInput Rate) : ICommand<Result<UpdatedResult>>;

public sealed record GpFundInterestLine(Guid AccountId, string EmployeeNumber, string EmployeeName, decimal Interest);
public sealed record PostGpFundInterestResult(string FiscalYear, decimal RatePercent, DateOnly PostedOn, int Accounts, decimal TotalInterest, int AlreadyPosted, bool DryRun, IReadOnlyList<GpFundInterestLine> Lines);

/// Credits a fiscal year's interest to every account open in it: rate / 12 on each month-end balance of the year
/// (interest of the year itself left out), posted on the year's last day. Accounts already credited are skipped.
public sealed record PostGpFundInterestCommand(Guid RateId, bool DryRun) : ICommand<Result<PostGpFundInterestResult>>;

public class GpFundInterestRateInputValidator : AbstractValidator<GpFundInterestRateInput>
{
  public GpFundInterestRateInputValidator()
  {
    RuleFor(x => x.FiscalYear).NotEmpty().MaximumLength(20);
    RuleFor(x => x.RatePercent).InclusiveBetween(0, 100);
    RuleFor(x => x.NotificationRef).MaximumLength(200);
    RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo.HasValue).WithMessage("The rate cannot end before it starts.");
  }
}

public class CreateGpFundInterestRateCommandValidator : AbstractValidator<CreateGpFundInterestRateCommand>
{
  public CreateGpFundInterestRateCommandValidator() => RuleFor(x => x.Rate).NotNull().SetValidator(new GpFundInterestRateInputValidator());
}

public class UpdateGpFundInterestRateCommandValidator : AbstractValidator<UpdateGpFundInterestRateCommand>
{
  public UpdateGpFundInterestRateCommandValidator() => RuleFor(x => x.Rate).NotNull().SetValidator(new GpFundInterestRateInputValidator());
}

public class GpFundHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetGpFundAccountsQuery, Result<GetGpFundAccountsQueryResult>>,
  IQueryHandler<GetGpFundAccountQuery, Result<GetGpFundAccountQueryResult>>,
  ICommandHandler<OpenGpFundAccountCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateGpFundAccountCommand, Result<UpdatedResult>>,
  ICommandHandler<PostGpFundTransactionCommand, Result<CreatedResult>>,
  ICommandHandler<CloseGpFundAccountCommand, Result<UpdatedResult>>,
  IQueryHandler<GetGpFundInterestRatesQuery, Result<GetGpFundInterestRatesQueryResult>>,
  ICommandHandler<CreateGpFundInterestRateCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateGpFundInterestRateCommand, Result<UpdatedResult>>,
  ICommandHandler<PostGpFundInterestCommand, Result<PostGpFundInterestResult>>
{
  // ---- accounts and ledger ----

  public async Task<Result<GetGpFundAccountsQueryResult>> Handle(GetGpFundAccountsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.GpFundAccounts.AsNoTracking();
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(a => a.EmployeeId == employeeId);
    }
    if (query.Status is { } status)
      rows = rows.Where(a => a.Status == status);

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderBy(a => a.AccountNumber).ThenBy(a => a.OpenedOn)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await MapAsync(page, cancellationToken);
    return Result<GetGpFundAccountsQueryResult>.Success(new(new PaginatedResult<GpFundAccountDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetGpFundAccountQueryResult>> Handle(GetGpFundAccountQuery query, CancellationToken cancellationToken)
  {
    GpFundAccount? account;
    if (query.Id is { } id)
      account = await context.LoadGpFundAccountAsync(id, cancellationToken);
    else
    {
      var employeeId = EmployeeId.Of(query.EmployeeId ?? throw new ArgumentException("Give the account or the employee."));
      account = await context.GpFundAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.EmployeeId == employeeId, cancellationToken)
        ?? throw new GpFundAccountNotFoundException("The employee has no GP Fund account.");
    }

    var transactions = await context.GpFundTransactions.AsNoTracking().Where(t => t.GpFundAccountId == account.Id)
      .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
    var dto = (await MapAsync([account], cancellationToken))[0];
    return Result<GetGpFundAccountQueryResult>.Success(new(dto, transactions.Select(t => t.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(OpenGpFundAccountCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    if (await context.GpFundAccounts.AnyAsync(a => a.EmployeeId == employee.Id, cancellationToken))
      return Result<CreatedResult>.Failure($"{employee.DisplayName} already has a GP Fund account.");
    if (command.OpenedOn > clock.Today)
      return Result<CreatedResult>.Failure("An account cannot be opened on a future date.");

    var account = GpFundAccount.Open(GpFundAccountId.New(), employee, command.AccountNumber, command.OpenedOn, command.MonthlySubscription);
    if (account.AccountNumber is { } number && await context.GpFundAccounts.AnyAsync(a => a.AccountNumber == number, cancellationToken))
      return Result<CreatedResult>.Failure($"GP Fund account number {number} is already in use.");

    context.GpFundAccounts.Add(account);
    if (command.OpeningBalance is > 0)
      context.GpFundTransactions.Add(GpFundTransaction.Opening(account, command.OpenedOn, command.OpeningBalance.Value, "Opening balance"));

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(account.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateGpFundAccountCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAccountAsync(command.Id, cancellationToken);
    account.Update(command.AccountNumber, command.MonthlySubscription);
    if (account.AccountNumber is { } number && await context.GpFundAccounts.AnyAsync(a => a.Id != account.Id && a.AccountNumber == number, cancellationToken))
      return Result<UpdatedResult>.Failure($"GP Fund account number {number} is already in use.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CreatedResult>> Handle(PostGpFundTransactionCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAccountAsync(command.AccountId, cancellationToken);
    account.EnsureOpen();
    if (command.TransactionDate > clock.Today)
      return Result<CreatedResult>.Failure("A GP Fund entry cannot be dated in the future.");

    var balance = await BalanceAsync(account.Id, cancellationToken);
    var amount = command.Amount ?? 0;
    var transaction = command.TransactionType switch
    {
      GpFundTransactionType.Opening => await context.GpFundTransactions.AnyAsync(t => t.GpFundAccountId == account.Id && t.TransactionType == GpFundTransactionType.Opening, cancellationToken)
        ? throw new DomainException("The account already has its opening balance; correct it with an adjustment.")
        : GpFundTransaction.Opening(account, command.TransactionDate, amount, command.Remarks),
      GpFundTransactionType.Interest => GpFundTransaction.Interest(account, command.TransactionDate, amount, command.Remarks),
      GpFundTransactionType.Withdrawal => GpFundTransaction.Withdrawal(account, command.TransactionDate, amount, balance, command.Remarks),
      GpFundTransactionType.FinalPayment => await FinalPaymentAsync(account, command.TransactionDate, balance, command.Remarks, cancellationToken),
      _ => balance + amount < 0
        ? throw new DomainException($"The adjustment would take the balance ({balance:N2}) below zero.")
        : GpFundTransaction.Adjustment(account, command.TransactionDate, amount, null, null, command.Remarks ?? "")
    };

    context.GpFundTransactions.Add(transaction);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(transaction.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(CloseGpFundAccountCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAccountAsync(command.Id, cancellationToken);
    account.Close(command.ClosedOn, await BalanceAsync(account.Id, cancellationToken));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- interest ----

  public async Task<Result<GetGpFundInterestRatesQueryResult>> Handle(GetGpFundInterestRatesQuery query, CancellationToken cancellationToken)
  {
    var rates = await context.GpFundInterestRates.AsNoTracking().OrderByDescending(r => r.EffectiveFrom).ToListAsync(cancellationToken);
    return Result<GetGpFundInterestRatesQueryResult>.Success(new(rates.Select(r => r.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateGpFundInterestRateCommand command, CancellationToken cancellationToken)
  {
    var i = command.Rate;
    var rate = GpFundInterestRate.Create(GpFundInterestRateId.New(), i.FiscalYear, i.RatePercent, i.NotificationRef, i.EffectiveFrom, i.EffectiveTo);
    if (await context.GpFundInterestRates.AnyAsync(r => r.FiscalYear == rate.FiscalYear, cancellationToken))
      return Result<CreatedResult>.Failure($"The rate for {rate.FiscalYear} is already recorded.");

    context.GpFundInterestRates.Add(rate);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(rate.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateGpFundInterestRateCommand command, CancellationToken cancellationToken)
  {
    var rate = await LoadRateAsync(command.Id, cancellationToken);
    if (await context.GpFundTransactions.AnyAsync(t => t.TransactionType == GpFundTransactionType.Interest && t.Remarks == InterestRemark(rate), cancellationToken))
      return Result<UpdatedResult>.Failure($"Interest for {rate.FiscalYear} has already been credited; correct it with adjustments.");

    var i = command.Rate;
    rate.Update(i.FiscalYear, i.RatePercent, i.NotificationRef, i.EffectiveFrom, i.EffectiveTo);
    if (await context.GpFundInterestRates.AnyAsync(r => r.Id != rate.Id && r.FiscalYear == rate.FiscalYear, cancellationToken))
      return Result<UpdatedResult>.Failure($"The rate for {rate.FiscalYear} is already recorded.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<PostGpFundInterestResult>> Handle(PostGpFundInterestCommand command, CancellationToken cancellationToken)
  {
    var rate = await LoadRateAsync(command.RateId, cancellationToken);
    if (rate.EffectiveTo is not { } yearEnd)
      return Result<PostGpFundInterestResult>.Failure($"The {rate.FiscalYear} rate has no end date; set the last day of the fiscal year first.");
    if (yearEnd > clock.Today)
      return Result<PostGpFundInterestResult>.Failure($"{rate.FiscalYear} has not ended yet (it ends on {yearEnd:yyyy-MM-dd}).");

    var yearStart = rate.EffectiveFrom;
    var remark = InterestRemark(rate);
    var accounts = await context.GpFundAccounts.AsNoTracking()
      .Where(a => a.OpenedOn <= yearEnd && (a.ClosedOn == null || a.ClosedOn >= yearStart))
      .ToListAsync(cancellationToken);
    var ids = accounts.Select(a => a.Id).ToList();
    var credited = (await context.GpFundTransactions.AsNoTracking()
        .Where(t => ids.Contains(t.GpFundAccountId) && t.TransactionType == GpFundTransactionType.Interest && t.Remarks == remark)
        .Select(t => t.GpFundAccountId).ToListAsync(cancellationToken))
      .ToHashSet();

    // the year's own interest is not part of the balance it is worked out on
    var ledger = (await context.GpFundTransactions.AsNoTracking()
        .Where(t => ids.Contains(t.GpFundAccountId) && t.TransactionDate <= yearEnd
          && !(t.TransactionType == GpFundTransactionType.Interest && t.TransactionDate > yearStart))
        .Select(t => new { t.GpFundAccountId, t.TransactionDate, t.Amount })
        .ToListAsync(cancellationToken))
      .GroupBy(t => t.GpFundAccountId).ToDictionary(g => g.Key, g => g.Select(t => (t.TransactionDate, t.Amount)).ToList());
    var people = await lookup.EmployeesAsync(accounts.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);

    var lines = new List<GpFundInterestLine>();
    foreach (var account in accounts.Where(a => !credited.Contains(a.Id) && a.Status == RecordStatus.Active))
    {
      var interest = GpFundInterest.ForYear(ledger.GetValueOrDefault(account.Id) ?? [], yearStart, yearEnd, rate.RatePercent);
      if (interest <= 0)
        continue;

      var person = people.GetValueOrDefault(account.EmployeeId.Value);
      lines.Add(new GpFundInterestLine(account.Id.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", interest));
      context.GpFundTransactions.Add(GpFundTransaction.Interest(account, yearEnd, interest, remark));
    }

    if (!command.DryRun)
      await context.SaveChangesAsync(cancellationToken);

    return Result<PostGpFundInterestResult>.Success(new(rate.FiscalYear, rate.RatePercent, yearEnd, lines.Count, lines.Sum(l => l.Interest), credited.Count,
      command.DryRun, lines.OrderBy(l => l.EmployeeNumber).ToList()));
  }

  /// The remark that marks a fiscal year's interest credit (and keeps it from being posted twice).
  private static string InterestRemark(GpFundInterestRate rate) => $"Interest for {rate.FiscalYear} @ {rate.RatePercent:0.##}%";

  private async Task<GpFundTransaction> FinalPaymentAsync(GpFundAccount account, DateOnly date, decimal balance, string? remarks, CancellationToken cancellationToken)
  {
    if (await context.Loans.AnyAsync(l => l.EmployeeId == account.EmployeeId && l.Status == LoanStatus.Active
        && context.LoanTypes.Any(t => t.Id == l.LoanTypeId && t.IsGpfAdvance), cancellationToken))
      throw new DomainException("A GP Fund advance is still being repaid; recover or settle it before the final payment.");

    return GpFundTransaction.FinalPayment(account, date, balance, remarks ?? "Final payment");
  }

  private async Task<GpFundAccount> LoadAccountAsync(Guid id, CancellationToken cancellationToken)
  {
    var accountId = GpFundAccountId.Of(id);
    return await context.GpFundAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
      ?? throw new GpFundAccountNotFoundException($"GP Fund account {id} was not found.");
  }

  private async Task<GpFundInterestRate> LoadRateAsync(Guid id, CancellationToken cancellationToken)
  {
    var rateId = GpFundInterestRateId.Of(id);
    return await context.GpFundInterestRates.FirstOrDefaultAsync(r => r.Id == rateId, cancellationToken)
      ?? throw new GpFundInterestRateNotFoundException($"GP Fund interest rate {id} was not found.");
  }

  private async Task<decimal> BalanceAsync(GpFundAccountId accountId, CancellationToken cancellationToken) =>
      await context.GpFundTransactions.Where(t => t.GpFundAccountId == accountId).SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;

  private async Task<List<GpFundAccountDto>> MapAsync(List<GpFundAccount> accounts, CancellationToken cancellationToken)
  {
    var ids = accounts.Select(a => a.Id.Value).ToList();
    var balances = await context.GpFundBalances.AsNoTracking().Where(b => ids.Contains(b.GpFundAccountId)).ToDictionaryAsync(b => b.GpFundAccountId, cancellationToken);
    var people = await lookup.EmployeesAsync(accounts.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    return accounts.Select(a =>
    {
      var person = people.GetValueOrDefault(a.EmployeeId.Value);
      var balance = balances.GetValueOrDefault(a.Id.Value);
      return new GpFundAccountDto(a.Id.Value, a.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", a.AccountNumber, a.OpenedOn, a.ClosedOn,
        a.MonthlySubscription, a.Status, balance?.Balance ?? 0, balance?.TotalSubscribed ?? 0, balance?.TotalInterest ?? 0);
    }).ToList();
  }
}
