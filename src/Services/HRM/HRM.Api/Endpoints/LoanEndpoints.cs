public sealed record SanctionLoanRequest(Guid LoanTypeId, decimal PrincipalAmount, decimal? InterestAmount, int InstallmentsCount, DateOnly StartDate, int? DeductionPriority);
public sealed record RepayLoanRequest(decimal Amount, DateOnly? PaidOn, string? Remarks);
public sealed record LoanPriorityRequest(int DeductionPriority);
public sealed record OpenGpFundAccountRequest(string? AccountNumber, DateOnly OpenedOn, decimal MonthlySubscription, decimal? OpeningBalance);
public sealed record UpdateGpFundAccountRequest(string? AccountNumber, decimal MonthlySubscription);
public sealed record GpFundTransactionRequest(GpFundTransactionType TransactionType, DateOnly TransactionDate, decimal? Amount, string? Remarks);
public sealed record CloseGpFundAccountRequest(DateOnly ClosedOn);
public sealed record PostInterestRequest(bool DryRun);
public sealed record CreateBankAccountRequest(string BankName, string? BranchName, string? AccountNumber, string? Iban, bool? MakePrimary);

/// Loans and advances with their installments, the GP Fund (accounts, ledger, yearly interest) and the salary bank
/// accounts. Payroll staff only.
public class LoanEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- loan types ----

    var types = app.MapGroup("/loan-types").WithTags("Loans");

    types.MapGet("/", async (bool? includeInactive, ISender sender) => (await sender.Send(new GetLoanTypesQuery(includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetLoanTypes")
      .Produces<GetLoanTypesQueryResult>()
      .WithSummary("Get Loan Types");

    types.MapPost("/", async (LoanTypeInput type, ISender sender) => (await sender.Send(new CreateLoanTypeCommand(type))).ToCreated(r => $"/loan-types/{r.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithName("CreateLoanType")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Loan Type")
      .WithDescription("salaryComponentId: the deduction line installments are recovered on (needed before loans of the type can be sanctioned). "
        + "defaultInterestRate: simple annual % over the term.");

    types.MapPut("/{id:guid}", async (Guid id, LoanTypeInput type, ISender sender) => (await sender.Send(new UpdateLoanTypeCommand(id, type))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateLoanType")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Loan Type")
      .WithDescription("Once loans exist, the deduction line and the GP Fund advance flag cannot change.");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      types.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetLoanTypeActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Edit)
        .WithName(active ? "ActivateLoanType" : "DeactivateLoanType")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(active ? "Activate Loan Type" : "Deactivate Loan Type");
    }

    // ---- loans ----

    var loans = app.MapGroup("/loans").WithTags("Loans");

    loans.MapGet("/", async (int? pageIndex, int? pageSize, Guid? employeeId, Guid? loanTypeId, string? status, ISender sender) =>
        (await sender.Send(new GetLoansQuery(QueryParsing.Page(pageIndex, pageSize), employeeId, loanTypeId, QueryParsing.ParseEnum<LoanStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetLoans")
      .Produces<GetLoansQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Loans")
      .WithDescription("Newest first. status = active | completed | cancelled | written_off.");

    loans.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetLoanQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetLoan")
      .Produces<GetLoanQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Loan")
      .WithDescription("With its installment schedule, what has been recovered and what is overdue.");

    app.MapPost("/employees/{id:guid}/loans", async (Guid id, SanctionLoanRequest r, ISender sender) =>
        (await sender.Send(new SanctionLoanCommand(id, r.LoanTypeId, r.PrincipalAmount, r.InterestAmount, r.InstallmentsCount, r.StartDate, r.DeductionPriority ?? 100)))
          .ToCreated(c => $"/loans/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithTags("Loans")
      .WithName("SanctionLoan")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Sanction Loan")
      .WithDescription("Equal monthly installments due from startDate (the last absorbs the rounding). Interest defaults to the type's rate over the term. "
        + "A GP Fund advance is paid out of the employee's fund (not more than its balance; one at a time). "
        + "deductionPriority (default 100): lower is recovered first when the pay cannot cover every deduction.");

    loans.MapPost("/{id:guid}/repay", async (Guid id, RepayLoanRequest r, ISender sender) => (await sender.Send(new RepayLoanCommand(id, r.Amount, r.PaidOn, r.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithName("RepayLoan")
      .Produces<LoanRepaymentResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Repayment")
      .WithDescription("Money paid back outside payroll, applied to the oldest open installments. A GP Fund advance repayment goes back into the fund.");

    loans.MapPut("/{id:guid}/priority", async (Guid id, LoanPriorityRequest r, ISender sender) =>
        (await sender.Send(new ChangeLoanPriorityCommand(id, r.DeductionPriority))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("ChangeLoanPriority")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Recovery Priority");

    loans.MapPost("/{id:guid}/installments/{installmentId:guid}/waive", async (Guid id, Guid installmentId, ISender sender) =>
        (await sender.Send(new WaiveInstallmentCommand(id, installmentId))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("WaiveInstallment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Waive Installment")
      .WithDescription("Forgives what is left of one installment (not for GP Fund advances).");

    foreach (var (action, route, summary, description) in new[]
    {
      (LoanAction.Cancel, "cancel", "Cancel Loan", "Withdraws a loan before anything was recovered. A GP Fund advance goes back into the fund."),
      (LoanAction.WriteOff, "write-off", "Write Off Loan", "Gives up the balance still owed; it stays on record (not for GP Fund advances).")
    })
    {
      loans.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new CloseLoanCommand(id, action, null))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Approve)
        .WithName($"{action}Loan")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(description);
    }

    // ---- GP Fund ----

    var gpf = app.MapGroup("/gp-fund").WithTags("GP Fund");

    gpf.MapGet("/accounts", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, ISender sender) =>
        (await sender.Send(new GetGpFundAccountsQuery(QueryParsing.Page(pageIndex, pageSize), employeeId, QueryParsing.ParseEnum<RecordStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetGpFundAccounts")
      .Produces<GetGpFundAccountsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get GP Fund Accounts")
      .WithDescription("With balances, total subscribed and total interest. status = active | inactive (closed).");

    gpf.MapGet("/accounts/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetGpFundAccountQuery(id, null))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetGpFundAccount")
      .Produces<GetGpFundAccountQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get GP Fund Account")
      .WithDescription("The account and its ledger, newest first.");

    app.MapGet("/employees/{id:guid}/gp-fund", async (Guid id, ISender sender) => (await sender.Send(new GetGpFundAccountQuery(null, id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("GP Fund")
      .WithName("GetEmployeeGpFund")
      .Produces<GetGpFundAccountQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Employee's GP Fund");

    app.MapPost("/employees/{id:guid}/gp-fund", async (Guid id, OpenGpFundAccountRequest r, ISender sender) =>
        (await sender.Send(new OpenGpFundAccountCommand(id, r.AccountNumber, r.OpenedOn, r.MonthlySubscription, r.OpeningBalance))).ToCreated(c => $"/gp-fund/accounts/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithTags("GP Fund")
      .WithName("OpenGpFundAccount")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Open GP Fund Account")
      .WithDescription("One account per employee. openingBalance (brought from the old register) becomes the first ledger row. "
        + "monthlySubscription is deducted by payroll each month.");

    gpf.MapPut("/accounts/{id:guid}", async (Guid id, UpdateGpFundAccountRequest r, ISender sender) =>
        (await sender.Send(new UpdateGpFundAccountCommand(id, r.AccountNumber, r.MonthlySubscription))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateGpFundAccount")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update GP Fund Account");

    gpf.MapPost("/accounts/{id:guid}/transactions", async (Guid id, GpFundTransactionRequest r, ISender sender) =>
        (await sender.Send(new PostGpFundTransactionCommand(id, r.TransactionType, r.TransactionDate, r.Amount, r.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("PostGpFundTransaction")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Post GP Fund Entry")
      .WithDescription("transactionType = opening | interest | withdrawal | final_payment | adjustment. A withdrawal cannot exceed the balance; the final payment "
        + "pays out the whole balance (no amount needed, not while an advance is being repaid); an adjustment is signed and needs a reason. "
        + "Ledger rows are never changed afterwards.");

    gpf.MapPost("/accounts/{id:guid}/close", async (Guid id, CloseGpFundAccountRequest r, ISender sender) =>
        (await sender.Send(new CloseGpFundAccountCommand(id, r.ClosedOn))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("CloseGpFundAccount")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Close GP Fund Account")
      .WithDescription("Only when the balance is zero (after the final payment).");

    gpf.MapGet("/interest-rates", async (ISender sender) => (await sender.Send(new GetGpFundInterestRatesQuery())).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetGpFundInterestRates")
      .Produces<GetGpFundInterestRatesQueryResult>()
      .WithSummary("Get GP Fund Interest Rates");

    gpf.MapPost("/interest-rates", async (GpFundInterestRateInput rate, ISender sender) =>
        (await sender.Send(new CreateGpFundInterestRateCommand(rate))).ToCreated(r => $"/gp-fund/interest-rates/{r.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("CreateGpFundInterestRate")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Record GP Fund Interest Rate")
      .WithDescription("The rate notified for a fiscal year (e.g. 2025-26, 1 July - 30 June). Periods may not overlap.");

    gpf.MapPut("/interest-rates/{id:guid}", async (Guid id, GpFundInterestRateInput rate, ISender sender) =>
        (await sender.Send(new UpdateGpFundInterestRateCommand(id, rate))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Approve)
      .WithName("UpdateGpFundInterestRate")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update GP Fund Interest Rate")
      .WithDescription("Not once the year's interest has been credited.");

    gpf.MapPost("/interest-rates/{id:guid}/post", async (Guid id, PostInterestRequest? r, ISender sender) =>
        (await sender.Send(new PostGpFundInterestCommand(id, r?.DryRun ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithName("PostGpFundInterest")
      .Produces<PostGpFundInterestResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Credit Year's GP Fund Interest")
      .WithDescription("After the fiscal year ends: rate / 12 on each month-end balance of the year, credited on its last day to every account open in it. "
        + "Accounts already credited are skipped, so it can run again; dryRun=true only lists the amounts.");

    // ---- bank accounts ----

    app.MapGet("/employees/{id:guid}/bank-accounts", async (Guid id, bool? includeInactive, ISender sender) =>
        (await sender.Send(new GetBankAccountsQuery(id, includeInactive ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Bank Accounts")
      .WithName("GetBankAccounts")
      .Produces<GetBankAccountsQueryResult>()
      .WithSummary("Get Employee Bank Accounts")
      .WithDescription("The salary (primary) account first.");

    app.MapPost("/employees/{id:guid}/bank-accounts", async (Guid id, CreateBankAccountRequest r, ISender sender) =>
        (await sender.Send(new CreateBankAccountCommand(id, new BankAccountInput(r.BankName, r.BranchName, r.AccountNumber, r.Iban), r.MakePrimary ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithTags("Bank Accounts")
      .WithName("CreateBankAccount")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Bank Account")
      .WithDescription("Needs the account number or the IBAN (PKxx 4-letter bank code + 16 digits, checked). The first account, or one with makePrimary, becomes the salary account.");

    var bank = app.MapGroup("/bank-accounts").WithTags("Bank Accounts");

    bank.MapPut("/{id:guid}", async (Guid id, BankAccountInput account, ISender sender) => (await sender.Send(new UpdateBankAccountCommand(id, account))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("UpdateBankAccount")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Bank Account")
      .WithDescription("Payments already made keep the account they were sent to.");

    bank.MapPost("/{id:guid}/make-primary", async (Guid id, ISender sender) => (await sender.Send(new MakeBankAccountPrimaryCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("MakeBankAccountPrimary")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Make Salary Account");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      bank.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetBankAccountActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Edit)
        .WithName(active ? "ActivateBankAccount" : "DeactivateBankAccount")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(active ? "Reopen Bank Account" : "Close Bank Account")
        .WithDescription(active ? "" : "Closing the salary account makes the newest other active account primary.");
    }
  }
}
