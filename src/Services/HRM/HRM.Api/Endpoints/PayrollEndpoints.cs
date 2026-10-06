public sealed record CreatePayrollPeriodRequest(int Year, int Month, DateOnly? StartDate, DateOnly? EndDate);
public sealed record CreatePayrollRunRequest(Guid PeriodId, PayrollRunType RunType, string? RunLabel, Guid? ReplacesRunId);
public sealed record RenamePayrollRunRequest(string? RunLabel);
public sealed record CalculatePayrollRunRequest(IReadOnlyList<Guid>? EmployeeIds);
public sealed record ReversePayrollRunRequest(string Reason);
public sealed record PayrollAdjustmentRequest(Guid EmployeeId, AdjustmentType AdjustmentType, decimal Amount, string Reason);
public sealed record HoldRequest(string? Remarks);
public sealed record GeneratePaymentsRequest(PaymentMethod? PaymentMethod);
public sealed record ProcessPaymentsRequest(DateOnly PaymentDate, string PaymentReference, IReadOnlyList<Guid>? PaymentIds);
public sealed record PaymentStatusRequest(string? PaymentReference);

/// Payroll: months, runs (draft -> calculated -> reviewed -> approved -> finalized -> paid; reversed), pay slips with
/// adjustments and holds, the pay register, and salary payments to the bank.
public class PayrollEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- periods ----

    var periods = app.MapGroup("/payroll-periods").WithTags("Payroll Periods");

    periods.MapGet("/", async (int? year, ISender sender) => (await sender.Send(new GetPayrollPeriodsQuery(year))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayrollPeriods")
      .Produces<GetPayrollPeriodsQueryResult>()
      .WithSummary("Get Payroll Months")
      .WithDescription("Newest first, with the number of runs in each.");

    periods.MapPost("/", async (CreatePayrollPeriodRequest r, ISender sender) =>
        (await sender.Send(new CreatePayrollPeriodCommand(r.Year, r.Month, r.StartDate, r.EndDate))).ToCreated(c => $"/payroll-periods/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithName("CreatePayrollPeriod")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Open Payroll Month")
      .WithDescription("The calendar month unless startDate / endDate are given (at most 31 days).");

    foreach (var (action, route, permission, summary, description) in new[]
    {
      (PayrollPeriodAction.Close, "close", PermissionCatalog.Payroll.Approve, "Close Payroll Month", "No new runs; refused while a run is not finalized."),
      (PayrollPeriodAction.Reopen, "reopen", PermissionCatalog.Payroll.Approve, "Reopen Payroll Month", "A closed (not locked) month."),
      (PayrollPeriodAction.Lock, "lock", PermissionCatalog.Payroll.Post, "Lock Payroll Month", "Final: a locked month never reopens and its runs can no longer be reversed.")
    })
    {
      periods.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new ChangePayrollPeriodStatusCommand(id, action))).ToOk())
        .RequirePermission(permission)
        .WithName($"{action}PayrollPeriod")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(description);
    }

    // ---- runs ----

    var runs = app.MapGroup("/payroll-runs").WithTags("Payroll Runs");

    runs.MapGet("/", async (Guid? periodId, int? year, string? status, string? runType, ISender sender) =>
        (await sender.Send(new GetPayrollRunsQuery(periodId, year, QueryParsing.ParseEnum<PayrollRunStatus>(status, "status"),
          QueryParsing.ParseEnum<PayrollRunType>(runType, "runType")))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayrollRuns")
      .Produces<GetPayrollRunsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Payroll Runs")
      .WithDescription("Newest first, with head count and totals. status = draft | calculated | reviewed | approved | finalized | paid | reversed; "
        + "runType = regular | supplementary | arrears | bonus | final_settlement.");

    runs.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPayrollRunQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayrollRun")
      .Produces<GetPayrollRunQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Payroll Run");

    runs.MapPost("/", async (CreatePayrollRunRequest r, ISender sender) =>
        (await sender.Send(new CreatePayrollRunCommand(r.PeriodId, r.RunType, r.RunLabel, r.ReplacesRunId))).ToCreated(c => $"/payroll-runs/{c.Id}"))
      .RequirePermission(PermissionCatalog.Payroll.Create)
      .WithName("CreatePayrollRun")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Create Payroll Run")
      .WithDescription("In an open month. One live regular run per month; supplementary, arrears, bonus and final settlement runs may repeat. "
        + "replacesRunId: the reversed run this one is made again for.");

    runs.MapPut("/{id:guid}", async (Guid id, RenamePayrollRunRequest r, ISender sender) => (await sender.Send(new RenamePayrollRunCommand(id, r.RunLabel))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("RenamePayrollRun")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Rename Payroll Run");

    runs.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeletePayrollRunCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Delete)
      .WithName("DeletePayrollRun")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Delete Payroll Run")
      .WithDescription("Only while a draft or calculated; its pay slips go with it.");

    runs.MapPost("/{id:guid}/calculate", async (Guid id, CalculatePayrollRunRequest? r, ISender sender) =>
        (await sender.Send(new CalculatePayrollRunCommand(id, r?.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("CalculatePayrollRun")
      .Produces<CalculatePayrollRunResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Calculate Payroll Run")
      .WithDescription("Regular: everyone holding a regular post during the month; supplementary: those not on the month's other runs; arrears, bonus and "
        + "final settlement: the employees given adjustments. Pay is split wherever the post, grade or pay changes, prorated by payable days (leave without "
        + "pay is not paid), with allowances from rules or overrides, then deductions, GP Fund, loan installments due and income tax. Adjustments are kept. "
        + "Employees that cannot be paid are listed in problems. Can be run again until the run is reviewed; employeeIds: only those.");

    foreach (var (step, route, permission, summary) in new[]
    {
      (PayrollRunStep.Review, "review", PermissionCatalog.Payroll.Edit, "Mark Run Reviewed"),
      (PayrollRunStep.SendBackToCalculated, "send-back", PermissionCatalog.Payroll.Edit, "Send Run Back for Changes"),
      (PayrollRunStep.Approve, "approve", PermissionCatalog.Payroll.Approve, "Approve Payroll Run"),
      (PayrollRunStep.SendBackToReview, "unapprove", PermissionCatalog.Payroll.Approve, "Withdraw Approval"),
      (PayrollRunStep.ResetToDraft, "reset", PermissionCatalog.Payroll.Edit, "Discard Calculation")
    })
    {
      runs.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new MovePayrollRunCommand(id, step))).ToOk())
        .RequirePermission(permission)
        .WithName($"{step}PayrollRun")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(step switch
        {
          PayrollRunStep.Approve => "A reviewed run. The person who prepared the payroll cannot approve it (unless Payroll:RequireSeparateApprover is off).",
          PayrollRunStep.ResetToDraft => "A calculated run goes back to draft; its pay slips and adjustments are deleted.",
          _ => ""
        });
    }

    runs.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender) => (await sender.Send(new FinalizePayrollRunCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithName("FinalizePayrollRun")
      .Produces<FinalizePayrollRunResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Finalize Payroll Run")
      .WithDescription("Posts an approved run in one step: tax withheld goes to the tax ledger, loan installments are recovered, GP Fund subscriptions and "
        + "advance recoveries are credited, final settlements are linked to their separations, and every pay slip is issued as a frozen snapshot. "
        + "From then on the run's figures cannot change.");

    runs.MapPost("/{id:guid}/reverse", async (Guid id, ReversePayrollRunRequest r, ISender sender) => (await sender.Send(new ReversePayrollRunCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithName("ReversePayrollRun")
      .Produces<ReversePayrollRunResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Reverse Payroll Run")
      .WithDescription("A finalized or paid run whose payments have not gone out (returned ones are fine; pending ones are cancelled). Loan recoveries "
        + "are undone (the installments become due again), GP Fund rows are offset, and its tax leaves the year-to-date. Not in a locked month.");

    runs.MapPost("/{id:guid}/mark-paid", async (Guid id, ISender sender) => (await sender.Send(new MarkPayrollRunPaidCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithName("MarkPayrollRunPaid")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Run Paid")
      .WithDescription("When every slip that is not held has a processed payment.");

    runs.MapGet("/{id:guid}/transactions", async (Guid id, int? pageIndex, int? pageSize, string? search, string? status, ISender sender) =>
        (await sender.Send(new GetPayrollTransactionsQuery(id, QueryParsing.Page(pageIndex, pageSize, 50), search,
          QueryParsing.ParseEnum<PayrollTransactionStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Pay Slips")
      .WithName("GetPayrollTransactions")
      .Produces<GetPayrollTransactionsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Run's Pay Slips")
      .WithDescription("By employee number. search: number or name. status = calculated | held | released. paymentStatus: of the latest payment.");

    runs.MapGet("/{id:guid}/register", async (Guid id, ISender sender) => (await sender.Send(new GetPayRegisterQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayRegister")
      .Produces<PayRegisterResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Pay Register")
      .WithDescription("One row per pay slip and one column per component (earnings, then deductions; ADJ_PAY / ADJ_RECOVERY for adjustments), with totals. "
        + "Issued slips appear as issued.");

    runs.MapPost("/{id:guid}/adjustments", async (Guid id, PayrollAdjustmentRequest r, ISender sender) =>
        (await sender.Send(new AddPayrollAdjustmentCommand(id, r.EmployeeId, r.AdjustmentType, r.Amount, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithTags("Pay Slips")
      .WithName("AddPayrollAdjustment")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Adjustment")
      .WithDescription("adjustmentType = arrears | bonus (positive) | recovery (negative) | correction | other. Positive amounts are taxable pay. "
        + "The employee is added to the run if needed and the slip is worked out again.");

    app.MapDelete("/payroll-adjustments/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new RemovePayrollAdjustmentCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithTags("Pay Slips")
      .WithName("RemovePayrollAdjustment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Remove Adjustment");

    var slips = app.MapGroup("/payroll-transactions").WithTags("Pay Slips");

    slips.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPayrollTransactionQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayrollTransaction")
      .Produces<GetPayrollTransactionQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Pay Slip Workings")
      .WithDescription("Segments, every line with how it was reached (rule, base, rate, formula, notification), loan deductions and adjustments.");

    slips.MapGet("/{id:guid}/payslip", async (Guid id, ISender sender) => (await sender.Send(new GetPayslipQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithName("GetPayslip")
      .Produces<PayslipDto>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Pay Slip")
      .WithDescription("The slip as issued at finalization (isFinal = true), or a preview of it before that.");

    slips.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new RemovePayrollTransactionCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Edit)
      .WithName("RemovePayrollTransaction")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Take Employee off Run")
      .WithDescription("Only while the run is a draft or calculated (a full recalculation of a regular run adds them back if they are due pay).");

    foreach (var (hold, route, summary) in new[] { (true, "hold", "Hold Salary"), (false, "release", "Release Salary") })
    {
      slips.MapPost($"/{{id:guid}}/{route}", async (Guid id, HoldRequest? r, ISender sender) => (await sender.Send(new HoldPayrollTransactionCommand(id, hold, r?.Remarks))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Approve)
        .WithName(hold ? "HoldPayrollTransaction" : "ReleasePayrollTransaction")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(hold ? "No payment is made while a salary is held (also after finalization)." : "");
    }

    // ---- payments ----

    runs.MapPost("/{id:guid}/payments", async (Guid id, GeneratePaymentsRequest? r, ISender sender) =>
        (await sender.Send(new GeneratePaymentsCommand(id, r?.PaymentMethod ?? PaymentMethod.BankTransfer))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithTags("Salary Payments")
      .WithName("GeneratePayments")
      .Produces<GeneratePaymentsResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Prepare Salary Payments")
      .WithDescription("A pending payment for each slip of a finalized run that is not held, has net pay and no live payment, into the salary account "
        + "(copied onto the payment). Slips without an account are listed in problems. Run again after a payment failed or came back.");

    runs.MapGet("/{id:guid}/payments", async (Guid id, string? status, ISender sender) =>
        (await sender.Send(new GetPaymentsQuery(id, QueryParsing.ParseEnum<PaymentStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Salary Payments")
      .WithName("GetPayments")
      .Produces<GetPaymentsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Salary Payments")
      .WithDescription("status = pending | processed | failed | returned.");

    runs.MapPost("/{id:guid}/payments/process", async (Guid id, ProcessPaymentsRequest r, ISender sender) =>
        (await sender.Send(new ProcessPaymentsCommand(id, r.PaymentDate, r.PaymentReference, r.PaymentIds))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.Post)
      .WithTags("Salary Payments")
      .WithName("ProcessPayments")
      .Produces<ProcessPaymentsResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Mark Payments Processed")
      .WithDescription("The bank confirmed the transfer: the run's pending payments (or paymentIds) are processed with the bank's reference.");

    runs.MapGet("/{id:guid}/bank-advice", async (Guid id, ISender sender) => (await sender.Send(new GetBankAdviceQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Payroll.View)
      .WithTags("Salary Payments")
      .WithName("GetBankAdvice")
      .Produces<BankAdviceResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Bank Advice")
      .WithDescription("The run's pending and processed payments bank by bank, with account, IBAN and amount.");

    foreach (var (action, route, summary) in new[] { (PaymentAction.Fail, "fail", "Payment Failed"), (PaymentAction.Return, "return", "Payment Returned") })
    {
      app.MapPost($"/payroll-payments/{{id:guid}}/{route}", async (Guid id, PaymentStatusRequest? r, ISender sender) =>
          (await sender.Send(new ChangePaymentStatusCommand(id, action, r?.PaymentReference))).ToOk())
        .RequirePermission(PermissionCatalog.Payroll.Post)
        .WithTags("Salary Payments")
        .WithName($"{action}Payment")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary)
        .WithDescription(action == PaymentAction.Fail ? "The bank refused a pending payment." : "A processed payment came back (closed account, wrong title).");
    }
  }
}
