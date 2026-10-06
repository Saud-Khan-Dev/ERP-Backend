using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// ---- runs ----

public sealed record GetPayrollRunsQueryResult(IReadOnlyList<PayrollRunDto> Runs);
public sealed record GetPayrollRunsQuery(Guid? PeriodId, int? Year, PayrollRunStatus? Status, PayrollRunType? RunType) : IQuery<Result<GetPayrollRunsQueryResult>>;

public sealed record GetPayrollRunQueryResult(PayrollRunDto Run);
public sealed record GetPayrollRunQuery(Guid Id) : IQuery<Result<GetPayrollRunQueryResult>>;

/// ReplacesRunId: the reversed run this one is made again for.
public sealed record CreatePayrollRunCommand(Guid PeriodId, PayrollRunType RunType, string? RunLabel, Guid? ReplacesRunId) : ICommand<Result<CreatedResult>>;

public sealed record RenamePayrollRunCommand(Guid Id, string? RunLabel) : ICommand<Result<UpdatedResult>>;

/// Deletes a run that is still a draft or calculated, with its slips.
public sealed record DeletePayrollRunCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public sealed record CalculatePayrollRunResult(int Calculated, int Removed, IReadOnlyList<string> Problems, PayrollRunDto Run);

/// Works out the run. Regular: everyone holding a regular post during the month. Supplementary: those not on the
/// month's other regular or supplementary runs (late joiners, missed cases). Arrears, bonus and final settlement: the
/// employees given adjustments. EmployeeIds: only those (the rest stay as they are).
public sealed record CalculatePayrollRunCommand(Guid Id, IReadOnlyList<Guid>? EmployeeIds) : ICommand<Result<CalculatePayrollRunResult>>;

public enum PayrollRunStep
{
  Review,
  SendBackToCalculated,
  Approve,
  SendBackToReview,
  ResetToDraft
}

public sealed record MovePayrollRunCommand(Guid Id, PayrollRunStep Step) : ICommand<Result<UpdatedResult>>;

public sealed record FinalizePayrollRunResult(int Slips, int LoanRecoveries, int GpFundEntries, int TaxEntries, decimal NetPayable);

/// Posts an approved run: tax withheld, loan recoveries, GP Fund subscriptions and advance recoveries are recorded, the
/// pay slips are issued, and the run's figures are locked.
public sealed record FinalizePayrollRunCommand(Guid Id) : ICommand<Result<FinalizePayrollRunResult>>;

public sealed record ReversePayrollRunResult(int LoanRecoveriesUndone, int GpFundEntries, int PaymentsCancelled);

/// Undoes a finalized or paid run before any of its money went out: loan recoveries and GP Fund rows are undone and
/// its tax drops out of the year-to-date. A new run can then replace it.
public sealed record ReversePayrollRunCommand(Guid Id, string Reason) : ICommand<Result<ReversePayrollRunResult>>;

public sealed record MarkPayrollRunPaidCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class CreatePayrollRunCommandValidator : AbstractValidator<CreatePayrollRunCommand>
{
  public CreatePayrollRunCommandValidator()
  {
    RuleFor(x => x.PeriodId).NotEmpty();
    RuleFor(x => x.RunType).IsInEnum();
    RuleFor(x => x.RunLabel).MaximumLength(150);
  }
}

public class ReversePayrollRunCommandValidator : AbstractValidator<ReversePayrollRunCommand>
{
  public ReversePayrollRunCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

// ---- pay slips of a run ----

public sealed record GetPayrollTransactionsQueryResult(PaginatedResult<PayrollTransactionSummaryDto> Transactions);
public sealed record GetPayrollTransactionsQuery(Guid RunId, PaginationRequest Pagination, string? Search, PayrollTransactionStatus? Status) : IQuery<Result<GetPayrollTransactionsQueryResult>>;

public sealed record GetPayrollTransactionQueryResult(PayrollTransactionDto Transaction);
public sealed record GetPayrollTransactionQuery(Guid Id) : IQuery<Result<GetPayrollTransactionQueryResult>>;

/// A hand-entered line on an employee's slip in the run (the employee is added to the run if needed); the slip is
/// worked out again. Positive: arrears, bonus, correction; negative: a recovery.
public sealed record AddPayrollAdjustmentCommand(Guid RunId, Guid EmployeeId, AdjustmentType AdjustmentType, decimal Amount, string Reason) : ICommand<Result<CreatedResult>>;

public sealed record RemovePayrollAdjustmentCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

/// Takes an employee off a run that is still a draft or calculated.
public sealed record RemovePayrollTransactionCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public sealed record HoldPayrollTransactionCommand(Guid Id, bool Hold, string? Remarks) : ICommand<Result<UpdatedResult>>;

/// The issued pay slip (frozen at finalization), or a preview while the run is open. OnlyIssued / OnlyForEmployee: for
/// self-service.
public sealed record GetPayslipQuery(Guid TransactionId, Guid? OnlyForEmployee = null, bool OnlyIssued = false) : IQuery<Result<PayslipDto>>;

public sealed record PayRegisterColumn(string Code, string Name, ComponentType ComponentType);

public sealed record PayRegisterRow(
  Guid PayrollTransactionId,
  string EmployeeNumber,
  string EmployeeName,
  string? PostCode,
  string? Designation,
  int? Bps,
  decimal? DaysPayable,
  IReadOnlyDictionary<string, decimal> Amounts,
  decimal GrossPay,
  decimal TotalDeductions,
  decimal NetPayable);

public sealed record PayRegisterResult(PayrollRunDto Run, IReadOnlyList<PayRegisterColumn> Columns, IReadOnlyList<PayRegisterRow> Rows, IReadOnlyDictionary<string, decimal> Totals);

/// Every slip of the run with one column per component (issued slips as issued).
public sealed record GetPayRegisterQuery(Guid RunId) : IQuery<Result<PayRegisterResult>>;

public class AddPayrollAdjustmentCommandValidator : AbstractValidator<AddPayrollAdjustmentCommand>
{
  public AddPayrollAdjustmentCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.AdjustmentType).IsInEnum();
    RuleFor(x => x.Amount).NotEqual(0);
    RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
  }
}

public class PayrollRunHandlers(
  IApplicationDbContext context,
  PayrollEngine engine,
  PayrollInputBuilder inputs,
  PayslipBuilder payslips,
  HrLookup lookup,
  ICurrentUser currentUser,
  IClock clock,
  IOptions<PayrollOptions> options) :
  IQueryHandler<GetPayrollRunsQuery, Result<GetPayrollRunsQueryResult>>,
  IQueryHandler<GetPayrollRunQuery, Result<GetPayrollRunQueryResult>>,
  ICommandHandler<CreatePayrollRunCommand, Result<CreatedResult>>,
  ICommandHandler<RenamePayrollRunCommand, Result<UpdatedResult>>,
  ICommandHandler<DeletePayrollRunCommand, Result<UpdatedResult>>,
  ICommandHandler<CalculatePayrollRunCommand, Result<CalculatePayrollRunResult>>,
  ICommandHandler<MovePayrollRunCommand, Result<UpdatedResult>>,
  ICommandHandler<FinalizePayrollRunCommand, Result<FinalizePayrollRunResult>>,
  ICommandHandler<ReversePayrollRunCommand, Result<ReversePayrollRunResult>>,
  ICommandHandler<MarkPayrollRunPaidCommand, Result<UpdatedResult>>,
  IQueryHandler<GetPayrollTransactionsQuery, Result<GetPayrollTransactionsQueryResult>>,
  IQueryHandler<GetPayrollTransactionQuery, Result<GetPayrollTransactionQueryResult>>,
  ICommandHandler<AddPayrollAdjustmentCommand, Result<CreatedResult>>,
  ICommandHandler<RemovePayrollAdjustmentCommand, Result<UpdatedResult>>,
  ICommandHandler<RemovePayrollTransactionCommand, Result<UpdatedResult>>,
  ICommandHandler<HoldPayrollTransactionCommand, Result<UpdatedResult>>,
  IQueryHandler<GetPayslipQuery, Result<PayslipDto>>,
  IQueryHandler<GetPayRegisterQuery, Result<PayRegisterResult>>
{
  // ---- runs ----

  public async Task<Result<GetPayrollRunsQueryResult>> Handle(GetPayrollRunsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.PayrollRuns.AsNoTracking();
    if (query.PeriodId is { } period)
    {
      var periodId = PayrollPeriodId.Of(period);
      rows = rows.Where(r => r.PayrollPeriodId == periodId);
    }
    if (query.Year is { } year)
      rows = rows.Where(r => context.PayrollPeriods.Any(p => p.Id == r.PayrollPeriodId && p.Year == year));
    if (query.Status is { } status)
      rows = rows.Where(r => r.Status == status);
    if (query.RunType is { } type)
      rows = rows.Where(r => r.RunType == type);

    var runs = await rows.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    return Result<GetPayrollRunsQueryResult>.Success(new(await MapRunsAsync(runs, cancellationToken)));
  }

  public async Task<Result<GetPayrollRunQueryResult>> Handle(GetPayrollRunQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(query.Id, cancellationToken);
    return Result<GetPayrollRunQueryResult>.Success(new((await MapRunsAsync([run], cancellationToken))[0]));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var period = await context.LoadPayrollPeriodAsync(command.PeriodId, cancellationToken);
    PayrollRun? replaces = null;
    if (command.ReplacesRunId is { } replaced)
    {
      replaces = await context.LoadPayrollRunAsync(replaced, cancellationToken);
      if (replaces.RunType != command.RunType)
        return Result<CreatedResult>.Failure($"The replacement must be {EnumText.WithArticle(replaces.RunType)} run, like the run it replaces.");
    }

    if (command.RunType == PayrollRunType.Regular
        && await context.PayrollRuns.AnyAsync(r => r.PayrollPeriodId == period.Id && r.RunType == PayrollRunType.Regular && r.Status != PayrollRunStatus.Reversed, cancellationToken))
      return Result<CreatedResult>.Failure($"{period.Label} already has a regular run. Reverse it before making another, or use a supplementary run.");

    var run = PayrollRun.Create(PayrollRunId.New(), period, command.RunType, command.RunLabel, replaces, currentUser.UserId);
    context.PayrollRuns.Add(run);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(run.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(RenamePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    run.Rename(command.RunLabel);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeletePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    run.EnsureEditable();
    context.PayrollTransactions.RemoveRange(await engine.SlipsAsync(run.Id, cancellationToken));
    context.PayrollRuns.Remove(run);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<CalculatePayrollRunResult>> Handle(CalculatePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    var period = await context.PayrollPeriods.FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    period.EnsureOpen();
    run.EnsureEditable();

    List<EmployeeId>? only = command.EmployeeIds is { Count: > 0 } given ? given.Select(EmployeeId.Of).ToList() : null;
    var slips = await engine.SlipsAsync(run.Id, cancellationToken, only);
    var problems = new List<string>();
    var removed = 0;

    HashSet<EmployeeId> targets;
    if (run.IsComputed)
    {
      var eligible = (await inputs.PostedEmployeesAsync(period, cancellationToken)).ToHashSet();
      if (run.RunType == PayrollRunType.Supplementary)
        eligible.ExceptWith(await PaidElsewhereAsync(run, cancellationToken));

      // a full recalculation drops slips of people no longer due pay here (unless HR entered adjustments on them)
      if (only is null)
      {
        foreach (var stale in slips.Where(s => !eligible.Contains(s.EmployeeId) && s.Adjustments.Count == 0).ToList())
        {
          context.PayrollTransactions.Remove(stale);
          slips.Remove(stale);
          removed++;
        }
      }

      targets = [.. eligible, .. slips.Select(s => s.EmployeeId)];
      if (only is not null)
        targets.IntersectWith(only);
      if (targets.Count == 0)
        problems.Add(run.RunType == PayrollRunType.Supplementary
          ? "Everyone holding a post this month is already on another run of the month."
          : "No one held a regular post during the month.");
    }
    else
    {
      targets = [.. slips.Select(s => s.EmployeeId)];
      if (targets.Count == 0)
        problems.Add($"{EnumText.WithArticle(run.RunType, capitalized: true)} run is made of adjustments: add them for each employee, then calculate.");
    }

    var employees = await context.Employees.AsNoTracking().Where(e => targets.Contains(e.Id)).ToListAsync(cancellationToken);
    problems.AddRange(await engine.CalculateAsync(run, period, slips, employees, cancellationToken));
    run.MarkCalculated(currentUser.UserId);
    await context.SaveChangesAsync(cancellationToken);

    var calculated = employees.Count - problems.Count(p => employees.Any(e => p.StartsWith(e.EmployeeNumber + ":", StringComparison.Ordinal)));
    return Result<CalculatePayrollRunResult>.Success(new(calculated, removed, problems, (await MapRunsAsync([run], cancellationToken))[0]));
  }

  public async Task<Result<UpdatedResult>> Handle(MovePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    switch (command.Step)
    {
      case PayrollRunStep.Review:
        if (!await context.PayrollTransactions.AnyAsync(t => t.PayrollRunId == run.Id, cancellationToken))
          return Result<UpdatedResult>.Failure("The run has no pay slips to review.");
        run.Review(currentUser.UserId);
        break;
      case PayrollRunStep.SendBackToCalculated:
        run.SendBackToCalculated();
        break;
      case PayrollRunStep.Approve:
        run.Approve(currentUser.UserId, clock.UtcNow, options.Value.RequireSeparateApprover);
        break;
      case PayrollRunStep.SendBackToReview:
        run.SendBackToReview();
        break;
      case PayrollRunStep.ResetToDraft:
        run.ResetToDraft();
        context.PayrollTransactions.RemoveRange(await engine.SlipsAsync(run.Id, cancellationToken));
        break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<FinalizePayrollRunResult>> Handle(FinalizePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    if (run.Status != PayrollRunStatus.Approved)
      return Result<FinalizePayrollRunResult>.Failure($"The run is {EnumText.Words(run.Status)}; only an approved run can be finalized.");

    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    var slips = await engine.SlipsAsync(run.Id, cancellationToken);
    if (slips.Count == 0)
      return Result<FinalizePayrollRunResult>.Failure("The run has no pay slips.");

    var now = clock.UtcNow;
    var postedOn = period.EndDate;
    var taxYear = await inputs.TaxYearAsync(period, cancellationToken);
    var taxable = await context.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.IsTaxable, cancellationToken);

    var loanIds = slips.SelectMany(s => s.LoanDeductions).Select(d => d.EmployeeLoanId).Distinct().ToList();
    var loans = await context.Loans.Include(l => l.Installments).Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
    var advanceTypes = (await context.LoanTypes.AsNoTracking().Where(t => t.IsGpfAdvance).Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();
    var employeeIds = slips.Select(s => s.EmployeeId).ToList();
    var accounts = await context.GpFundAccounts.AsNoTracking().Where(a => employeeIds.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId, cancellationToken);

    await using var transaction = await context.BeginTransactionAsync(cancellationToken);
    int recoveries = 0, fundRows = 0, taxRows = 0;

    foreach (var slip in slips)
    {
      if (taxYear is not null)
      {
        var income = slip.Lines.Where(l => l.ComponentType == ComponentType.Earning && taxable.GetValueOrDefault(l.SalaryComponentId)).Sum(l => l.CalculatedAmount)
          + slip.TaxableAdjustments;
        var withheld = slip.Lines.Where(l => l.Source == ComponentSource.Tax).Sum(l => l.CalculatedAmount);
        if (income > 0 || withheld > 0)
        {
          context.TaxLedger.Add(EmployeeTaxLedgerEntry.Create(slip.EmployeeId, taxYear.Id, slip.Id, income, withheld));
          taxRows++;
        }
      }

      accounts.TryGetValue(slip.EmployeeId, out var account);
      foreach (var deduction in slip.LoanDeductions)
      {
        var loan = loans[deduction.EmployeeLoanId];
        loan.RecordPayrollRecovery(deduction.InstallmentId, deduction.InstallmentAmount);
        recoveries++;
        if (advanceTypes.Contains(loan.LoanTypeId) && account is not null)
        {
          context.GpFundTransactions.Add(GpFundTransaction.AdvanceRecovery(account, postedOn, deduction.InstallmentAmount, loan.Id, slip.Id,
            $"Advance recovered in the {period.Label} payroll"));
          fundRows++;
        }
      }

      var subscription = slip.Lines.Where(l => l.Source == ComponentSource.Gpf).Sum(l => l.CalculatedAmount);
      if (subscription > 0 && account is not null)
      {
        context.GpFundTransactions.Add(GpFundTransaction.Subscription(account, postedOn, subscription, slip.Id, $"Subscription for {period.Label}"));
        fundRows++;
      }
    }

    if (run.RunType == PayrollRunType.FinalSettlement)
    {
      var separations = await context.Separations.Where(s => employeeIds.Contains(s.EmployeeId) && s.SettlementPayrollTransactionId == null).ToListAsync(cancellationToken);
      foreach (var separation in separations)
        separation.LinkSettlement(slips.First(s => s.EmployeeId == separation.EmployeeId).Id);
    }

    // the ledgers are written while the run is still approved: once it is finalized the database refuses them
    await context.SaveChangesAsync(cancellationToken);

    foreach (var (slipId, slip) in await payslips.BuildAsync(run, period, slips, isFinal: true, now, cancellationToken))
      context.Payslips.Add(Payslip.Issue(slipId, PayslipBuilder.Serialize(slip), now));
    run.FinalizeRun(now);
    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Result<FinalizePayrollRunResult>.Success(new(slips.Count, recoveries, fundRows, taxRows, slips.Sum(s => s.NetPayable)));
  }

  public async Task<Result<ReversePayrollRunResult>> Handle(ReversePayrollRunCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    if (period.Status == PayrollPeriodStatus.Locked)
      return Result<ReversePayrollRunResult>.Failure($"{period.Label} is locked; its payroll can no longer be reversed.");

    var slips = await context.PayrollTransactions.AsNoTracking().Include(t => t.Lines).Include(t => t.LoanDeductions).AsSplitQuery()
      .Where(t => t.PayrollRunId == run.Id).ToListAsync(cancellationToken);
    var slipIds = slips.Select(s => s.Id).ToList();
    var payments = await context.Payments.Where(p => slipIds.Contains(p.PayrollTransactionId)).ToListAsync(cancellationToken);
    var processed = payments.Count(p => p.PaymentStatus == PaymentStatus.Processed);
    if (processed > 0)
      return Result<ReversePayrollRunResult>.Failure($"{processed} salary payment(s) of this run went out. Record them as returned before reversing the run.");

    run.Reverse();
    var today = clock.Today;
    var reason = $"{period.Label} payroll reversed ({command.Reason.Trim()})";

    var cancelled = 0;
    foreach (var payment in payments.Where(p => p.PaymentStatus == PaymentStatus.Pending))
    {
      payment.Fail("Run reversed");
      cancelled++;
    }

    var loanIds = slips.SelectMany(s => s.LoanDeductions).Select(d => d.EmployeeLoanId).Distinct().ToList();
    var loans = await context.Loans.Include(l => l.Installments).Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
    var advanceTypes = (await context.LoanTypes.AsNoTracking().Where(t => t.IsGpfAdvance).Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();
    var employeeIds = slips.Select(s => s.EmployeeId).ToList();
    var accounts = await context.GpFundAccounts.AsNoTracking().Where(a => employeeIds.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId, cancellationToken);

    int undone = 0, fundRows = 0;
    foreach (var slip in slips)
    {
      accounts.TryGetValue(slip.EmployeeId, out var account);
      foreach (var deduction in slip.LoanDeductions)
      {
        var loan = loans[deduction.EmployeeLoanId];
        loan.UndoPayrollRecovery(deduction.InstallmentId, deduction.InstallmentAmount);
        undone++;
        if (advanceTypes.Contains(loan.LoanTypeId) && account is not null)
        {
          context.GpFundTransactions.Add(GpFundTransaction.Adjustment(account, today, -deduction.InstallmentAmount, loan.Id, slip.Id, $"{reason}: advance recovery undone"));
          fundRows++;
        }
      }

      var subscription = slip.Lines.Where(l => l.Source == ComponentSource.Gpf).Sum(l => l.CalculatedAmount);
      if (subscription > 0 && account is not null)
      {
        context.GpFundTransactions.Add(GpFundTransaction.Adjustment(account, today, -subscription, null, slip.Id, $"{reason}: subscription undone"));
        fundRows++;
      }
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<ReversePayrollRunResult>.Success(new(undone, fundRows, cancelled));
  }

  public async Task<Result<UpdatedResult>> Handle(MarkPayrollRunPaidCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.Id, cancellationToken);
    var payable = await context.PayrollTransactions.AsNoTracking()
      .Where(t => t.PayrollRunId == run.Id && t.Status != PayrollTransactionStatus.Held && t.NetPayable > 0)
      .Select(t => t.Id).ToListAsync(cancellationToken);
    var paid = await context.Payments.AsNoTracking()
      .Where(p => payable.Contains(p.PayrollTransactionId) && p.PaymentStatus == PaymentStatus.Processed)
      .Select(p => p.PayrollTransactionId).Distinct().CountAsync(cancellationToken);
    if (paid < payable.Count)
      return Result<UpdatedResult>.Failure($"{payable.Count - paid} pay slip(s) have no processed payment yet.");

    run.MarkPaid(clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- pay slips of a run ----

  public async Task<Result<GetPayrollTransactionsQueryResult>> Handle(GetPayrollTransactionsQuery query, CancellationToken cancellationToken)
  {
    var runId = PayrollRunId.Of(query.RunId);
    var rows = context.PayrollTransactions.AsNoTracking().Where(t => t.PayrollRunId == runId);
    if (query.Status is { } status)
      rows = rows.Where(t => t.Status == status);
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var pattern = SearchPattern.Contains(query.Search);
      rows = rows.Where(t => context.Employees.Any(e => e.Id == t.EmployeeId
        && (EF.Functions.Like(e.EmployeeNumber.ToLower(), pattern, SearchPattern.Escape) || EF.Functions.Like(e.FullName!.ToLower(), pattern, SearchPattern.Escape))));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await (
        from t in rows
        join e in context.Employees on t.EmployeeId equals e.Id
        orderby e.EmployeeNumber
        select t)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await SummariesAsync(page, cancellationToken);
    return Result<GetPayrollTransactionsQueryResult>.Success(new(new PaginatedResult<PayrollTransactionSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetPayrollTransactionQueryResult>> Handle(GetPayrollTransactionQuery query, CancellationToken cancellationToken)
  {
    var slip = await LoadSlipAsync(query.Id, cancellationToken);
    var summary = (await SummariesAsync([slip], cancellationToken))[0];
    var components = await context.SalaryComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
    var postCodes = await lookup.PostCodesAsync(slip.Segments.Select(s => (PostId?)s.PostId), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);

    return Result<GetPayrollTransactionQueryResult>.Success(new(new PayrollTransactionDto(
      summary,
      slip.Segments.OrderBy(s => s.PeriodFrom).Select(s => new PayrollSegmentDto(s.Id.Value, s.PostId.Value, postCodes.GetValueOrDefault(s.PostId.Value), s.GradeId.Value,
        grades.TryGetValue(s.GradeId.Value, out var grade) ? grade.BpsNumber : null, s.PayScaleStageId?.Value, s.BasicPay, s.PeriodFrom, s.PeriodTo, s.Days)).ToList(),
      slip.Lines.OrderBy(l => l.ComponentType).ThenBy(l => l.Source).ThenBy(l => components[l.SalaryComponentId].ComponentName).Select(l =>
        new PayrollLineDto(l.Id.Value, l.SegmentId?.Value, l.SalaryComponentId.Value, components[l.SalaryComponentId].ComponentCode, components[l.SalaryComponentId].ComponentName,
          l.SalaryComponentRuleId?.Value, l.Source, l.ComponentType, l.CalculationBase, l.BaseAmount, l.Rate, l.FormulaReference, l.CalculatedAmount,
          l.NotificationRef, l.EffectiveDate)).ToList(),
      slip.LoanDeductions.Select(d => new PayrollLoanDeductionDto(d.Id.Value, d.EmployeeLoanId.Value, d.InstallmentId.Value, d.InstallmentAmount)).ToList(),
      slip.Adjustments.Select(a => new PayrollAdjustmentDto(a.Id.Value, a.AdjustmentType, a.Amount, a.Reason, a.CreatedBy, a.CreatedAt)).ToList())));
  }

  public async Task<Result<CreatedResult>> Handle(AddPayrollAdjustmentCommand command, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(command.RunId, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    period.EnsureOpen();
    run.EnsureEditable();
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);

    var slips = await engine.SlipsAsync(run.Id, cancellationToken, [employee.Id]);
    var slip = slips.FirstOrDefault();
    if (slip is null)
    {
      if (run.RunType == PayrollRunType.Supplementary && (await PaidElsewhereAsync(run, cancellationToken)).Contains(employee.Id))
        return Result<CreatedResult>.Failure($"{employee.DisplayName} is already on another run of {period.Label}; add the adjustment there.");
      slip = PayrollTransaction.Open(PayrollTransactionId.New(), run, employee);
      context.PayrollTransactions.Add(slip);
      slips.Add(slip);
    }

    var adjustment = slip.AddAdjustment(command.AdjustmentType, command.Amount, command.Reason);
    var problem = (await engine.CalculateAsync(run, period, slips, [employee], cancellationToken)).FirstOrDefault(p => p.StartsWith(employee.EmployeeNumber + ":", StringComparison.Ordinal));
    if (problem is not null)
      return Result<CreatedResult>.Failure(problem);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(adjustment.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(RemovePayrollAdjustmentCommand command, CancellationToken cancellationToken)
  {
    var adjustmentId = PayrollAdjustmentId.Of(command.Id);
    var slipId = await context.PayrollAdjustments.AsNoTracking().Where(a => a.Id == adjustmentId).Select(a => a.PayrollTransactionId).FirstOrDefaultAsync(cancellationToken)
      ?? throw new PayrollTransactionNotFoundException($"Adjustment {command.Id} was not found.");
    var owner = await context.PayrollTransactions.AsNoTracking().FirstAsync(t => t.Id == slipId, cancellationToken);
    var run = await context.PayrollRuns.FirstAsync(r => r.Id == owner.PayrollRunId, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    period.EnsureOpen();
    run.EnsureEditable();

    var slips = await engine.SlipsAsync(run.Id, cancellationToken, [owner.EmployeeId]);
    var slip = slips.Single();
    slip.RemoveAdjustment(adjustmentId);

    // a slip of an adjustments-only run without adjustments has nothing left on it
    if (!run.IsComputed && slip.Adjustments.Count == 0)
      context.PayrollTransactions.Remove(slip);
    else
    {
      var employee = await context.Employees.AsNoTracking().FirstAsync(e => e.Id == slip.EmployeeId, cancellationToken);
      var problem = (await engine.CalculateAsync(run, period, slips, [employee], cancellationToken)).FirstOrDefault(p => p.StartsWith(employee.EmployeeNumber + ":", StringComparison.Ordinal));
      if (problem is not null)
        return Result<UpdatedResult>.Failure(problem);
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(RemovePayrollTransactionCommand command, CancellationToken cancellationToken)
  {
    var slipId = PayrollTransactionId.Of(command.Id);
    var owner = await context.PayrollTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == slipId, cancellationToken)
      ?? throw new PayrollTransactionNotFoundException($"Pay slip {command.Id} was not found.");
    var run = await context.PayrollRuns.AsNoTracking().FirstAsync(r => r.Id == owner.PayrollRunId, cancellationToken);
    run.EnsureEditable();

    context.PayrollTransactions.RemoveRange(await engine.SlipsAsync(run.Id, cancellationToken, [owner.EmployeeId]));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(HoldPayrollTransactionCommand command, CancellationToken cancellationToken)
  {
    var slipId = PayrollTransactionId.Of(command.Id);
    var slip = await context.PayrollTransactions.FirstOrDefaultAsync(t => t.Id == slipId, cancellationToken)
      ?? throw new PayrollTransactionNotFoundException($"Pay slip {command.Id} was not found.");
    var run = await context.PayrollRuns.AsNoTracking().FirstAsync(r => r.Id == slip.PayrollRunId, cancellationToken);
    if (run.Status == PayrollRunStatus.Reversed)
      return Result<UpdatedResult>.Failure("The run is reversed.");

    if (command.Hold)
    {
      if (await context.Payments.AnyAsync(p => p.PayrollTransactionId == slip.Id
          && (p.PaymentStatus == PaymentStatus.Pending || p.PaymentStatus == PaymentStatus.Processed), cancellationToken))
        return Result<UpdatedResult>.Failure("A payment of this salary is pending or made; mark it failed or returned before holding the salary.");
      slip.Hold(command.Remarks);
    }
    else
    {
      slip.Release(command.Remarks);
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<PayslipDto>> Handle(GetPayslipQuery query, CancellationToken cancellationToken)
  {
    var slip = await LoadSlipAsync(query.TransactionId, cancellationToken);
    if (query.OnlyForEmployee is { } own && slip.EmployeeId.Value != own)
      throw new PayslipNotFoundException($"Pay slip {query.TransactionId} was not found.");

    var run = await context.PayrollRuns.AsNoTracking().FirstAsync(r => r.Id == slip.PayrollRunId, cancellationToken);
    var issued = await context.Payslips.AsNoTracking().FirstOrDefaultAsync(p => p.PayrollTransactionId == slip.Id, cancellationToken);
    if (query.OnlyIssued && (issued is null || run.Status == PayrollRunStatus.Reversed))
      throw new PayslipNotFoundException($"Pay slip {query.TransactionId} was not found.");
    if (issued is not null)
      return Result<PayslipDto>.Success(PayslipBuilder.Deserialize(issued.SnapshotJson));

    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    var preview = await payslips.BuildAsync(run, period, [slip], isFinal: false, null, cancellationToken);
    return Result<PayslipDto>.Success(preview[slip.Id]);
  }

  public async Task<Result<PayRegisterResult>> Handle(GetPayRegisterQuery query, CancellationToken cancellationToken)
  {
    var run = await context.LoadPayrollRunAsync(query.RunId, cancellationToken);
    var period = await context.PayrollPeriods.AsNoTracking().FirstAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
    var slipIds = await context.PayrollTransactions.AsNoTracking().Where(t => t.PayrollRunId == run.Id).Select(t => t.Id).ToListAsync(cancellationToken);

    var issued = (await context.Payslips.AsNoTracking().Where(p => slipIds.Contains(p.PayrollTransactionId)).ToListAsync(cancellationToken))
      .ToDictionary(p => p.PayrollTransactionId, p => PayslipBuilder.Deserialize(p.SnapshotJson));
    var pending = slipIds.Where(id => !issued.ContainsKey(id)).ToList();
    if (pending.Count > 0)
    {
      var slips = await context.PayrollTransactions.AsNoTracking()
        .Include(t => t.Segments).Include(t => t.Lines).Include(t => t.LoanDeductions).Include(t => t.Adjustments).AsSplitQuery()
        .Where(t => pending.Contains(t.Id)).ToListAsync(cancellationToken);
      foreach (var (id, slip) in await payslips.BuildAsync(run, period, slips, isFinal: false, null, cancellationToken))
        issued[id] = slip;
    }

    var all = issued.Values.OrderBy(s => s.EmployeeNumber).ToList();
    var columns = all.SelectMany(s => s.Earnings.Select(l => new PayRegisterColumn(Key(l, true), l.ComponentName, ComponentType.Earning)))
      .Concat(all.SelectMany(s => s.Deductions.Select(l => new PayRegisterColumn(Key(l, false), l.ComponentName, ComponentType.Deduction))))
      .DistinctBy(c => c.Code)
      .OrderBy(c => c.ComponentType).ThenBy(c => c.Code == SystemComponents.BasicPay ? 0 : 1).ThenBy(c => c.Name)
      .ToList();

    var rows = all.Select(s =>
    {
      var amounts = new Dictionary<string, decimal>();
      foreach (var line in s.Earnings)
        amounts[Key(line, true)] = amounts.GetValueOrDefault(Key(line, true)) + line.Amount;
      foreach (var line in s.Deductions)
        amounts[Key(line, false)] = amounts.GetValueOrDefault(Key(line, false)) + line.Amount;
      return new PayRegisterRow(s.PayrollTransactionId, s.EmployeeNumber, s.EmployeeName, s.PostCode, s.Designation, s.Bps, s.DaysPayable, amounts,
        s.GrossPay, s.TotalDeductions, s.NetPayable);
    }).ToList();

    var totals = columns.ToDictionary(c => c.Code, c => rows.Sum(r => r.Amounts.GetValueOrDefault(c.Code)));
    totals["GROSS"] = rows.Sum(r => r.GrossPay);
    totals["DEDUCTIONS"] = rows.Sum(r => r.TotalDeductions);
    totals["NET"] = rows.Sum(r => r.NetPayable);

    return Result<PayRegisterResult>.Success(new((await MapRunsAsync([run], cancellationToken))[0], columns, rows, totals));
  }

  /// Adjustments show as one column on each side.
  private static string Key(PayslipLineDto line, bool earning) => line.ComponentCode == "ADJ" ? (earning ? "ADJ_PAY" : "ADJ_RECOVERY") : line.ComponentCode;

  /// Employees already on another live regular or supplementary run of the month.
  private async Task<List<EmployeeId>> PaidElsewhereAsync(PayrollRun run, CancellationToken cancellationToken) =>
      await context.PayrollTransactions.AsNoTracking()
        .Where(t => t.PayrollRunId != run.Id && context.PayrollRuns.Any(r => r.Id == t.PayrollRunId && r.PayrollPeriodId == run.PayrollPeriodId
          && r.Status != PayrollRunStatus.Reversed && (r.RunType == PayrollRunType.Regular || r.RunType == PayrollRunType.Supplementary)))
        .Select(t => t.EmployeeId).Distinct().ToListAsync(cancellationToken);

  private async Task<PayrollTransaction> LoadSlipAsync(Guid id, CancellationToken cancellationToken)
  {
    var slipId = PayrollTransactionId.Of(id);
    return await context.PayrollTransactions.AsNoTracking()
        .Include(t => t.Segments).Include(t => t.Lines).Include(t => t.LoanDeductions).Include(t => t.Adjustments).AsSplitQuery()
        .FirstOrDefaultAsync(t => t.Id == slipId, cancellationToken)
      ?? throw new PayrollTransactionNotFoundException($"Pay slip {id} was not found.");
  }

  private async Task<List<PayrollTransactionSummaryDto>> SummariesAsync(List<PayrollTransaction> slips, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(slips.Select(s => (EmployeeId?)s.EmployeeId), cancellationToken);
    var ids = slips.Select(s => s.Id).ToList();
    var payments = (await context.Payments.AsNoTracking().Where(p => ids.Contains(p.PayrollTransactionId))
        .Select(p => new { p.PayrollTransactionId, p.PaymentStatus, p.CreatedAt }).ToListAsync(cancellationToken))
      .GroupBy(p => p.PayrollTransactionId)
      .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First().PaymentStatus);

    return slips.Select(s =>
    {
      var person = people.GetValueOrDefault(s.EmployeeId.Value);
      return new PayrollTransactionSummaryDto(s.Id.Value, s.PayrollRunId.Value, s.EmployeeId.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "",
        s.DaysPayable, s.GrossPay, s.TotalDeductions, s.NetPayable, s.Status, payments.TryGetValue(s.Id, out var status) ? status : null, s.Remarks);
    }).ToList();
  }

  private async Task<List<PayrollRunDto>> MapRunsAsync(List<PayrollRun> runs, CancellationToken cancellationToken)
  {
    var ids = runs.Select(r => r.Id).ToList();
    var periodIds = runs.Select(r => r.PayrollPeriodId).Distinct().ToList();
    var periods = await context.PayrollPeriods.AsNoTracking().Where(p => periodIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
    var totals = await context.PayrollTransactions.AsNoTracking().Where(t => ids.Contains(t.PayrollRunId))
      .GroupBy(t => t.PayrollRunId)
      .Select(g => new
      {
        RunId = g.Key,
        Count = g.Count(),
        Held = g.Count(t => t.Status == PayrollTransactionStatus.Held),
        Gross = g.Sum(t => t.GrossPay),
        Deductions = g.Sum(t => t.TotalDeductions),
        Net = g.Sum(t => t.NetPayable)
      })
      .ToDictionaryAsync(x => x.RunId, cancellationToken);

    return runs.Select(r =>
    {
      var total = totals.GetValueOrDefault(r.Id);
      return new PayrollRunDto(r.Id.Value, r.PayrollPeriodId.Value, periods[r.PayrollPeriodId].Label, r.RunType, r.RunLabel, r.Status, r.ReversesRunId?.Value,
        r.PreparedBy, r.ReviewedBy, r.ApprovedBy, r.ApprovalDate, r.FinalizationDate, r.PaymentDate, total?.Count ?? 0, total?.Held ?? 0,
        total?.Gross ?? 0, total?.Deductions ?? 0, total?.Net ?? 0, r.CreatedAt);
    }).ToList();
  }
}
