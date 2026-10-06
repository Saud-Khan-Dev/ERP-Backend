using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record BankAccountInput(string BankName, string? BranchName, string? AccountNumber, string? Iban);

public sealed record GetBankAccountsQueryResult(IReadOnlyList<BankAccountDto> BankAccounts);
public sealed record GetBankAccountsQuery(Guid EmployeeId, bool IncludeInactive) : IQuery<Result<GetBankAccountsQueryResult>>;

/// Adds an account. The employee's first account, or one marked primary, becomes the salary account.
public sealed record CreateBankAccountCommand(Guid EmployeeId, BankAccountInput Account, bool MakePrimary) : ICommand<Result<CreatedResult>>;

public sealed record UpdateBankAccountCommand(Guid Id, BankAccountInput Account) : ICommand<Result<UpdatedResult>>;
public sealed record MakeBankAccountPrimaryCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// Closes or reopens an account. Closing the salary account makes the newest other active account primary.
public sealed record SetBankAccountActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class BankAccountInputValidator : AbstractValidator<BankAccountInput>
{
  public BankAccountInputValidator()
  {
    RuleFor(x => x.BankName).NotEmpty().MaximumLength(150);
    RuleFor(x => x.BranchName).MaximumLength(150);
    RuleFor(x => x.AccountNumber).MaximumLength(34);
    RuleFor(x => x.Iban).MaximumLength(34);
    RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.AccountNumber) || !string.IsNullOrWhiteSpace(x.Iban))
      .WithMessage("Enter the account number or the IBAN.");
  }
}

public class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
  public CreateBankAccountCommandValidator() => RuleFor(x => x.Account).NotNull().SetValidator(new BankAccountInputValidator());
}

public class UpdateBankAccountCommandValidator : AbstractValidator<UpdateBankAccountCommand>
{
  public UpdateBankAccountCommandValidator() => RuleFor(x => x.Account).NotNull().SetValidator(new BankAccountInputValidator());
}

public class BankAccountHandlers(IApplicationDbContext context) :
  IQueryHandler<GetBankAccountsQuery, Result<GetBankAccountsQueryResult>>,
  ICommandHandler<CreateBankAccountCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateBankAccountCommand, Result<UpdatedResult>>,
  ICommandHandler<MakeBankAccountPrimaryCommand, Result<UpdatedResult>>,
  ICommandHandler<SetBankAccountActivationCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetBankAccountsQueryResult>> Handle(GetBankAccountsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var rows = await context.BankAccounts.AsNoTracking()
      .Where(a => a.EmployeeId == employeeId && (query.IncludeInactive || a.Status == RecordStatus.Active))
      .OrderByDescending(a => a.IsPrimary).ThenByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
    return Result<GetBankAccountsQueryResult>.Success(new(rows.Select(a => a.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateBankAccountCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    var i = command.Account;
    var account = EmployeeBankAccount.Create(BankAccountId.New(), employee.Id, i.BankName, i.BranchName, i.AccountNumber, i.Iban);

    var accounts = await AccountsOfAsync(employee.Id, cancellationToken);
    if (Duplicate(accounts, account) is { } same)
      return Result<CreatedResult>.Failure($"This account is already recorded for the employee ({same.BankName}).");

    context.BankAccounts.Add(account);
    if (command.MakePrimary || !accounts.Any(a => a.IsPrimary && a.IsUsable))
      EmployeeBankAccounts.MakePrimary([.. accounts, account], account);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(account.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateBankAccountCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAsync(command.Id, cancellationToken);
    var i = command.Account;
    account.Update(i.BankName, i.BranchName, i.AccountNumber, i.Iban);
    if (Duplicate(await AccountsOfAsync(account.EmployeeId, cancellationToken), account) is { } same)
      return Result<UpdatedResult>.Failure($"This account is already recorded for the employee ({same.BankName}).");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MakeBankAccountPrimaryCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAsync(command.Id, cancellationToken);
    EmployeeBankAccounts.MakePrimary(await AccountsOfAsync(account.EmployeeId, cancellationToken), account);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetBankAccountActivationCommand command, CancellationToken cancellationToken)
  {
    var account = await LoadAsync(command.Id, cancellationToken);
    var accounts = await AccountsOfAsync(account.EmployeeId, cancellationToken);
    if (command.IsActive)
      account.Activate();
    else
      account.Deactivate();
    EmployeeBankAccounts.PromoteNextPrimary(accounts);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<EmployeeBankAccount> LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    var accountId = BankAccountId.Of(id);
    return await context.BankAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
      ?? throw new BankAccountNotFoundException($"Bank account {id} was not found.");
  }

  /// Tracked, so the primary flag moves across them in one save.
  private Task<List<EmployeeBankAccount>> AccountsOfAsync(EmployeeId employeeId, CancellationToken cancellationToken) =>
      context.BankAccounts.Where(a => a.EmployeeId == employeeId).ToListAsync(cancellationToken);

  private static EmployeeBankAccount? Duplicate(IEnumerable<EmployeeBankAccount> accounts, EmployeeBankAccount account) =>
      accounts.FirstOrDefault(a => a.Id != account.Id && a.IsUsable
        && ((account.Iban is not null && a.Iban == account.Iban)
          || (account.AccountNumber is not null && a.AccountNumber == account.AccountNumber && a.BankName.Equals(account.BankName, StringComparison.OrdinalIgnoreCase))));
}
