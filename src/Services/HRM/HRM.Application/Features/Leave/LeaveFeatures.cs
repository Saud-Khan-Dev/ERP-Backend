using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- leave types ----

public sealed record LeaveTypeInput(string Name, bool IsPaid, decimal? MaxDaysPerYear, string? AccrualRule, bool CarryForwardAllowed, bool AffectsPayroll, bool RequiresDocument = false);

public sealed record GetLeaveTypesQueryResult(IReadOnlyList<LeaveTypeDto> LeaveTypes);
public sealed record GetLeaveTypesQuery : IQuery<Result<GetLeaveTypesQueryResult>>;
public sealed record CreateLeaveTypeCommand(LeaveTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateLeaveTypeCommand(Guid Id, LeaveTypeInput Type) : ICommand<Result<UpdatedResult>>;
/// An inactive leave type is no longer offered for new applications; old ones keep it.
public sealed record SetLeaveTypeActiveCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;
/// Deletes a leave type nobody has used (no application, entitlement or ledger row); a used one is deactivated instead.
public sealed record DeleteLeaveTypeCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class LeaveTypeInputValidator : AbstractValidator<LeaveTypeInput>
{
  public LeaveTypeInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.MaxDaysPerYear).InclusiveBetween(0, 366).When(x => x.MaxDaysPerYear.HasValue);
    RuleFor(x => x.AccrualRule).MaximumLength(1000);
  }
}

public class CreateLeaveTypeCommandValidator : AbstractValidator<CreateLeaveTypeCommand>
{
  public CreateLeaveTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new LeaveTypeInputValidator());
}

public class UpdateLeaveTypeCommandValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
  public UpdateLeaveTypeCommandValidator() => RuleFor(x => x.Type).NotNull().SetValidator(new LeaveTypeInputValidator());
}

// ---- entitlements, balances, ledger ----

public sealed record GetLeaveEntitlementsQueryResult(IReadOnlyList<LeaveEntitlementDto> Entitlements);
public sealed record GetLeaveEntitlementsQuery(Guid EmployeeId, int? Year) : IQuery<Result<GetLeaveEntitlementsQueryResult>>;

/// Sets an employee's entitlement of a leave type for a year (creates it or changes the days).
public sealed record SetLeaveEntitlementCommand(Guid EmployeeId, Guid LeaveTypeId, int Year, decimal EntitledDays) : ICommand<Result<CreatedResult>>;

public sealed record GrantEntitlementsResult(int Granted, int AlreadyHad);

/// Grants a year's entitlement of a leave type to every employee in service (or the ones given). Days default to the
/// type's days per year. Existing entitlements are left alone.
public sealed record GrantLeaveEntitlementsCommand(int Year, Guid LeaveTypeId, decimal? EntitledDays, IReadOnlyList<Guid>? EmployeeIds) : ICommand<Result<GrantEntitlementsResult>>;

public sealed record GetLeaveBalancesQueryResult(Guid EmployeeId, int Year, IReadOnlyList<LeaveBalanceDto> Balances);
public sealed record GetLeaveBalancesQuery(Guid EmployeeId, int? Year) : IQuery<Result<GetLeaveBalancesQueryResult>>;

public sealed record GetLeaveLedgerQueryResult(IReadOnlyList<LeaveLedgerEntryDto> Entries);
public sealed record GetLeaveLedgerQuery(Guid EmployeeId, int? Year, Guid? LeaveTypeId) : IQuery<Result<GetLeaveLedgerQueryResult>>;

/// A hand-made ledger row: opening balance, accrual, encashment (days paid out) or a signed adjustment.
public sealed record PostLeaveLedgerEntryCommand(
  Guid EmployeeId,
  Guid LeaveTypeId,
  int Year,
  LeaveTransactionType TransactionType,
  decimal Days,
  DateOnly? TransactionDate,
  string? Remarks) : ICommand<Result<CreatedResult>>;

/// A leave application still waiting for a decision in the year being closed.
public sealed record PendingLeaveDto(Guid Id, Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? LeaveType, DateOnly StartDate, DateOnly EndDate, decimal Days);

public sealed record LeaveYearEndResult(int Year, int CarriedForward, int Lapsed, decimal DaysCarried, decimal DaysLapsed, bool DryRun,
  IReadOnlyList<PendingLeaveDto>? PendingApplications = null);

/// Closes a leave year: what is left of each balance is carried into the next year (types that allow it) or lapses.
/// Balances already closed are skipped, so it can be run again safely. Applications of that year still pending are
/// listed in the preview and must be decided first (closing would lapse the days they hold).
public sealed record RunLeaveYearEndCommand(int Year, bool DryRun) : ICommand<Result<LeaveYearEndResult>>;

public class SetLeaveEntitlementCommandValidator : AbstractValidator<SetLeaveEntitlementCommand>
{
  public SetLeaveEntitlementCommandValidator()
  {
    RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    RuleFor(x => x.EntitledDays).InclusiveBetween(0, 366);
  }
}

public class GrantLeaveEntitlementsCommandValidator : AbstractValidator<GrantLeaveEntitlementsCommand>
{
  public GrantLeaveEntitlementsCommandValidator()
  {
    RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    RuleFor(x => x.EntitledDays).InclusiveBetween(0, 366).When(x => x.EntitledDays.HasValue);
  }
}

public class PostLeaveLedgerEntryCommandValidator : AbstractValidator<PostLeaveLedgerEntryCommand>
{
  public PostLeaveLedgerEntryCommandValidator()
  {
    RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    RuleFor(x => x.TransactionType)
      .Must(t => t is LeaveTransactionType.Opening or LeaveTransactionType.Accrual or LeaveTransactionType.Encashment or LeaveTransactionType.Adjustment)
      .WithMessage("Post an opening, accrual, encashment or adjustment; usage, reversals, carry-forward and lapse come from applications and the year-end.");
    RuleFor(x => x.Days).NotEqual(0);
    RuleFor(x => x.Remarks).MaximumLength(2000);
  }
}

// ---- applications ----

/// supportingDocumentId: one of the employee's own documents (uploaded first); needed when the leave type asks for a paper.
public sealed record LeaveApplicationInput(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, decimal? Days, string? Reason, Guid? SupportingDocumentId = null);

public sealed record GetLeaveApplicationsQueryResult(PaginatedResult<LeaveApplicationDto> LeaveApplications);

public sealed record GetLeaveApplicationsQuery(
  PaginationRequest Pagination,
  Guid? EmployeeId,
  LeaveStatus? Status,
  Guid? LeaveTypeId,
  DateOnly? From,
  DateOnly? To,
  Guid? OrgUnitId) : IQuery<Result<GetLeaveApplicationsQueryResult>>;

public sealed record GetLeaveApplicationQueryResult(LeaveApplicationDto LeaveApplication);
public sealed record GetLeaveApplicationQuery(Guid Id) : IQuery<Result<GetLeaveApplicationQueryResult>>;

public sealed record LeaveDayCountResult(decimal Days, IReadOnlyList<LeaveDaysByYear> ByYear);

/// How many leave days a range would charge (the employee's working days without holidays).
public sealed record CountLeaveDaysQuery(Guid EmployeeId, DateOnly StartDate, DateOnly EndDate) : IQuery<Result<LeaveDayCountResult>>;

/// Applies for leave (HR on behalf of the employee, or the employee through /me). Days default to the working days in
/// the range; a half day is days = 0.5 on one date. Leave drawn from a balance must fit what is available.
public sealed record ApplyLeaveCommand(Guid EmployeeId, LeaveApplicationInput Leave) : ICommand<Result<CreatedResult>>;

public sealed record ChangeLeaveApplicationCommand(Guid Id, LeaveApplicationInput Leave, Guid? OnlyForEmployee = null) : ICommand<Result<UpdatedResult>>;

public enum LeaveDecision
{
  Approve,
  Reject,
  Cancel
}

/// Approve / reject (approvers), or cancel. OnlyForEmployee: self-service may only act on its own applications.
/// Reason: required to reject, and to cancel an approved leave; kept with who decided and when.
public sealed record DecideLeaveCommand(Guid Id, LeaveDecision Decision, Guid? OnlyForEmployee = null, string? Reason = null) : ICommand<Result<UpdatedResult>>;

public class DecideLeaveCommandValidator : AbstractValidator<DecideLeaveCommand>
{
  public DecideLeaveCommandValidator()
  {
    RuleFor(x => x.Decision).IsInEnum();
    RuleFor(x => x.Reason).MaximumLength(1000);
    RuleFor(x => x.Reason).NotEmpty().When(x => x.Decision == LeaveDecision.Reject).WithMessage("Give the reason for rejecting the leave; the employee sees it.");
  }
}

public class LeaveApplicationInputValidator : AbstractValidator<LeaveApplicationInput>
{
  public LeaveApplicationInputValidator()
  {
    RuleFor(x => x.LeaveTypeId).NotEmpty();
    RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("The leave cannot end before it starts.");
    RuleFor(x => x.Days).GreaterThan(0).When(x => x.Days.HasValue);
    RuleFor(x => x.Reason).MaximumLength(2000);
    RuleFor(x => x).Must(x => x.EndDate.DayNumber - x.StartDate.DayNumber < 366).WithMessage("A leave application covers at most a year.");
  }
}

public class ApplyLeaveCommandValidator : AbstractValidator<ApplyLeaveCommand>
{
  public ApplyLeaveCommandValidator() => RuleFor(x => x.Leave).NotNull().SetValidator(new LeaveApplicationInputValidator());
}

public class ChangeLeaveApplicationCommandValidator : AbstractValidator<ChangeLeaveApplicationCommand>
{
  public ChangeLeaveApplicationCommandValidator() => RuleFor(x => x.Leave).NotNull().SetValidator(new LeaveApplicationInputValidator());
}

public class LeaveHandlers(IApplicationDbContext context, LeaveService leave, HrLookup lookup, ICurrentUser currentUser, IClock clock, DutyGuard duty, IAuditTrail audit, OfficeScope scope) :
  IQueryHandler<GetLeaveTypesQuery, Result<GetLeaveTypesQueryResult>>,
  ICommandHandler<CreateLeaveTypeCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateLeaveTypeCommand, Result<UpdatedResult>>,
  ICommandHandler<SetLeaveTypeActiveCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteLeaveTypeCommand, Result<UpdatedResult>>,
  IQueryHandler<GetLeaveEntitlementsQuery, Result<GetLeaveEntitlementsQueryResult>>,
  ICommandHandler<SetLeaveEntitlementCommand, Result<CreatedResult>>,
  ICommandHandler<GrantLeaveEntitlementsCommand, Result<GrantEntitlementsResult>>,
  IQueryHandler<GetLeaveBalancesQuery, Result<GetLeaveBalancesQueryResult>>,
  IQueryHandler<GetLeaveLedgerQuery, Result<GetLeaveLedgerQueryResult>>,
  ICommandHandler<PostLeaveLedgerEntryCommand, Result<CreatedResult>>,
  ICommandHandler<RunLeaveYearEndCommand, Result<LeaveYearEndResult>>,
  IQueryHandler<GetLeaveApplicationsQuery, Result<GetLeaveApplicationsQueryResult>>,
  IQueryHandler<GetLeaveApplicationQuery, Result<GetLeaveApplicationQueryResult>>,
  IQueryHandler<CountLeaveDaysQuery, Result<LeaveDayCountResult>>,
  ICommandHandler<ApplyLeaveCommand, Result<CreatedResult>>,
  ICommandHandler<ChangeLeaveApplicationCommand, Result<UpdatedResult>>,
  ICommandHandler<DecideLeaveCommand, Result<UpdatedResult>>
{
  // ---- leave types ----

  public async Task<Result<GetLeaveTypesQueryResult>> Handle(GetLeaveTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.LeaveTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
    return Result<GetLeaveTypesQueryResult>.Success(new(rows.Select(t => t.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateLeaveTypeCommand command, CancellationToken cancellationToken)
  {
    var i = command.Type;
    var type = LeaveType.Create(LeaveTypeId.New(), i.Name, i.IsPaid, i.MaxDaysPerYear, i.AccrualRule, i.CarryForwardAllowed, i.AffectsPayroll, i.RequiresDocument);
    if (await context.LeaveTypes.AnyAsync(t => t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A leave type named '{type.Name}' already exists.");

    context.LeaveTypes.Add(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(type.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateLeaveTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadLeaveTypeAsync(command.Id, cancellationToken);
    var i = command.Type;

    // a type already used changes what payroll and balances mean; refuse the two switches that would rewrite history
    var used = await context.LeaveApplications.AnyAsync(a => a.LeaveTypeId == type.Id, cancellationToken);
    if (used && (type.AffectsPayroll != i.AffectsPayroll || type.IsPaid != i.IsPaid))
      return Result<UpdatedResult>.Failure($"'{type.Name}' is already used on leave applications; whether it is paid or deducted from pay cannot change. Create a new type instead.");

    type.Update(i.Name, i.IsPaid, i.MaxDaysPerYear, i.AccrualRule, i.CarryForwardAllowed, i.AffectsPayroll, i.RequiresDocument);
    if (await context.LeaveTypes.AnyAsync(t => t.Id != type.Id && t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another leave type is named '{type.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetLeaveTypeActiveCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadLeaveTypeAsync(command.Id, cancellationToken);
    type.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteLeaveTypeCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadLeaveTypeAsync(command.Id, cancellationToken);
    if (await context.LeaveApplications.AnyAsync(a => a.LeaveTypeId == type.Id, cancellationToken)
        || await context.LeaveEntitlements.AnyAsync(e => e.LeaveTypeId == type.Id, cancellationToken)
        || await context.LeaveLedger.AnyAsync(l => l.LeaveTypeId == type.Id, cancellationToken))
      throw new InUseException($"'{type.Name}' is used on leave records, so it cannot be deleted. Deactivate it instead.");

    context.LeaveTypes.Remove(type);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- entitlements, balances, ledger ----

  public async Task<Result<GetLeaveEntitlementsQueryResult>> Handle(GetLeaveEntitlementsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var rows = await context.LeaveEntitlements.AsNoTracking()
      .Where(e => e.EmployeeId == employeeId && (query.Year == null || e.Year == query.Year))
      .OrderByDescending(e => e.Year).ToListAsync(cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    return Result<GetLeaveEntitlementsQueryResult>.Success(new(rows.Select(e =>
      new LeaveEntitlementDto(e.Id.Value, e.EmployeeId.Value, e.LeaveTypeId.Value, types.GetValueOrDefault(e.LeaveTypeId), e.Year, e.EntitledDays)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(SetLeaveEntitlementCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var type = await context.LoadLeaveTypeAsync(command.LeaveTypeId, cancellationToken);
    var existing = await context.LeaveEntitlements.FirstOrDefaultAsync(e => e.EmployeeId == employee.Id && e.LeaveTypeId == type.Id && e.Year == command.Year, cancellationToken);

    if (existing is not null)
    {
      var ledger = await context.LeaveLedger.Where(l => l.EmployeeId == employee.Id && l.LeaveTypeId == type.Id && l.Year == command.Year)
        .SumAsync(l => (decimal?)l.Days, cancellationToken) ?? 0;
      existing.ChangeDays(command.EntitledDays, ledger);
      await context.SaveChangesAsync(cancellationToken);
      return CommandResults.Created(existing.Id);
    }

    employee.EnsureProfileActive();
    var entitlement = LeaveEntitlement.Create(LeaveEntitlementId.New(), employee.Id, type, command.Year, command.EntitledDays);
    context.LeaveEntitlements.Add(entitlement);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(entitlement.Id);
  }

  public async Task<Result<GrantEntitlementsResult>> Handle(GrantLeaveEntitlementsCommand command, CancellationToken cancellationToken)
  {
    var type = await context.LoadLeaveTypeAsync(command.LeaveTypeId, cancellationToken);
    var days = command.EntitledDays ?? type.MaxDaysPerYear
      ?? throw new DomainException($"'{type.Name}' has no days per year; give the days to grant.");

    type.EnsureActive();
    var inService = EmploymentStatuses.InService;
    var employees = context.Employees.AsNoTracking().Where(e => e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus));
    if (command.EmployeeIds is { Count: > 0 })
    {
      var only = command.EmployeeIds.Select(EmployeeId.Of).ToList();
      employees = employees.Where(e => only.Contains(e.Id));
    }

    var ids = await employees.Select(e => e.Id).ToListAsync(cancellationToken);
    var existing = (await context.LeaveEntitlements.Where(e => ids.Contains(e.EmployeeId) && e.LeaveTypeId == type.Id && e.Year == command.Year)
      .ToListAsync(cancellationToken)).ToDictionary(e => e.EmployeeId);
    var ledger = (await context.LeaveLedger.Where(l => ids.Contains(l.EmployeeId) && l.LeaveTypeId == type.Id && l.Year == command.Year)
        .GroupBy(l => l.EmployeeId).Select(g => new { EmployeeId = g.Key, Total = g.Sum(l => l.Days) }).ToListAsync(cancellationToken))
      .ToDictionary(x => x.EmployeeId, x => x.Total);

    var granted = 0;
    foreach (var id in ids)
    {
      if (existing.TryGetValue(id, out var entitlement))
      {
        // a 0-day row the system made for a carried or opening balance is filled in, not skipped
        if (entitlement.Grant(days, ledger.GetValueOrDefault(id)))
          granted++;
        continue;
      }

      context.LeaveEntitlements.Add(LeaveEntitlement.Create(LeaveEntitlementId.New(), id, type, command.Year, days));
      granted++;
    }

    using (audit.Quietly())
    {
      var where = scope.IsLimited ? $" · offices: {await scope.OfficeNamesAsync(cancellationToken)}" : "";
      audit.Record(AuditActions.Ran, HrmRuns.LeaveEntitlements, type.Id.Value, command.Year.ToString(), type.Name,
        detail: $"{type.Name} {command.Year}: {days:0.##} day(s) each · {granted} granted · {ids.Count - granted} already had them{where}");
      await context.SaveChangesAsync(cancellationToken);
    }
    return Result<GrantEntitlementsResult>.Success(new(granted, ids.Count - granted));
  }

  public async Task<Result<GetLeaveBalancesQueryResult>> Handle(GetLeaveBalancesQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
      throw new EmployeeNotFoundException($"Employee {query.EmployeeId} was not found.");

    var year = query.Year ?? clock.Today.Year;
    var balances = await context.LeaveBalances.AsNoTracking().Where(b => b.EmployeeId == employeeId.Value && b.Year == year).ToListAsync(cancellationToken);
    var pending = (await context.LeaveApplications.AsNoTracking()
        .Where(a => a.EmployeeId == employeeId && a.Status == LeaveStatus.Pending && a.StartDate.Year == year)
        .ToListAsync(cancellationToken))
      .GroupBy(a => a.LeaveTypeId.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Days));
    var types = await context.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, t => t.Name, cancellationToken);

    var data = balances.OrderBy(b => types.GetValueOrDefault(b.LeaveTypeId)).Select(b =>
    {
      var waiting = pending.GetValueOrDefault(b.LeaveTypeId);
      return new LeaveBalanceDto(b.LeaveTypeId, types.GetValueOrDefault(b.LeaveTypeId), b.Year, b.EntitledDays, b.AccruedDays, b.UsedDays, b.BalanceDays, waiting, b.BalanceDays - waiting);
    }).ToList();
    return Result<GetLeaveBalancesQueryResult>.Success(new(employeeId.Value, year, data));
  }

  public async Task<Result<GetLeaveLedgerQueryResult>> Handle(GetLeaveLedgerQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var rows = context.LeaveLedger.AsNoTracking().Where(l => l.EmployeeId == employeeId);
    if (query.Year is { } year)
      rows = rows.Where(l => l.Year == year);
    if (query.LeaveTypeId is { } type)
    {
      var typeId = LeaveTypeId.Of(type);
      rows = rows.Where(l => l.LeaveTypeId == typeId);
    }

    var list = await rows.OrderByDescending(l => l.TransactionDate).ThenByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    return Result<GetLeaveLedgerQueryResult>.Success(new(list.Select(l => l.ToDto(types.GetValueOrDefault(l.LeaveTypeId))).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(PostLeaveLedgerEntryCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    await duty.EnsureNotOwnRecordAsync(employee.Id, "correct the leave balance", employee, cancellationToken);
    var type = await context.LoadLeaveTypeAsync(command.LeaveTypeId, cancellationToken);
    var date = command.TransactionDate ?? clock.Today;

    var entry = command.TransactionType switch
    {
      LeaveTransactionType.Opening => LeaveLedgerEntry.Opening(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks),
      LeaveTransactionType.Accrual => LeaveLedgerEntry.Accrual(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks),
      LeaveTransactionType.Encashment => LeaveLedgerEntry.Encashment(employee.Id, type.Id, command.Year, Math.Abs(command.Days), date, command.Remarks),
      _ => LeaveLedgerEntry.Adjustment(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks)
    };

    // the balance a ledger row belongs to needs its entitlement row (balances are read from entitlements); a 0-day
    // placeholder, which granting the year fills in later
    if (!await context.LeaveEntitlements.AnyAsync(e => e.EmployeeId == employee.Id && e.LeaveTypeId == type.Id && e.Year == command.Year, cancellationToken))
      context.LeaveEntitlements.Add(LeaveEntitlement.Placeholder(LeaveEntitlementId.New(), employee.Id, type, command.Year));

    if (entry.Days < 0)
    {
      var balance = await leave.BalanceAsync(employee.Id, type.Id, command.Year, null, cancellationToken);
      if (balance.Entitled + balance.Ledger + entry.Days < 0)
        return Result<CreatedResult>.Failure($"Only {balance.Entitled + balance.Ledger:0.##} day(s) of {type.Name} are left in {command.Year}.");
    }

    context.LeaveLedger.Add(entry);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(entry.Id);
  }

  public async Task<Result<LeaveYearEndResult>> Handle(RunLeaveYearEndCommand command, CancellationToken cancellationToken)
  {
    var year = command.Year;
    if (year >= clock.Today.Year)
      return Result<LeaveYearEndResult>.Failure($"{year} has not ended yet.");

    // applications of that year still waiting: closing would lapse the days they hold, so they are decided first
    var pendingRows = await context.LeaveApplications.AsNoTracking()
      .Where(a => a.Status == LeaveStatus.Pending && a.StartDate.Year <= year && a.EndDate.Year >= year)
      .OrderBy(a => a.StartDate).ToListAsync(cancellationToken);
    var pendingPeople = await lookup.EmployeesAsync(pendingRows.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    var pendingTypes = await TypeNamesAsync(cancellationToken);
    var pending = pendingRows.Select(a => new PendingLeaveDto(a.Id.Value, a.EmployeeId.Value, pendingPeople.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
      pendingPeople.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", pendingTypes.GetValueOrDefault(a.LeaveTypeId), a.StartDate, a.EndDate, a.Days)).ToList();
    if (!command.DryRun && pending.Count > 0)
      return Result<LeaveYearEndResult>.Failure($"{pending.Count} leave application(s) of {year} are still waiting for a decision. Approve or reject them before closing the year.");

    var closed = (await context.LeaveLedger.AsNoTracking()
        .Where(l => l.Year == year && (l.TransactionType == LeaveTransactionType.Lapse || l.TransactionType == LeaveTransactionType.CarryForward))
        .Select(l => new { l.EmployeeId, l.LeaveTypeId }).Distinct().ToListAsync(cancellationToken))
      .Select(x => (x.EmployeeId.Value, x.LeaveTypeId.Value)).ToHashSet();
    var carriedInto = (await context.LeaveLedger.AsNoTracking()
        .Where(l => l.Year == year + 1 && l.TransactionType == LeaveTransactionType.CarryForward)
        .Select(l => new { l.EmployeeId, l.LeaveTypeId }).Distinct().ToListAsync(cancellationToken))
      .Select(x => (x.EmployeeId.Value, x.LeaveTypeId.Value)).ToHashSet();

    var balances = await context.LeaveBalances.AsNoTracking().Where(b => b.Year == year && b.BalanceDays > 0).ToListAsync(cancellationToken);
    var types = await context.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, cancellationToken);
    var nextYear = await context.LeaveEntitlements.Where(e => e.Year == year + 1).Select(e => new { e.EmployeeId, e.LeaveTypeId }).ToListAsync(cancellationToken);
    var hasNext = nextYear.Select(x => (x.EmployeeId.Value, x.LeaveTypeId.Value)).ToHashSet();

    var yearEnd = new DateOnly(year, 12, 31);
    int carried = 0, lapsed = 0;
    decimal daysCarried = 0, daysLapsed = 0;

    foreach (var balance in balances)
    {
      var key = (balance.EmployeeId, balance.LeaveTypeId);
      if (closed.Contains(key) || carriedInto.Contains(key))
        continue;

      var type = types[balance.LeaveTypeId];
      var employeeId = EmployeeId.Of(balance.EmployeeId);
      var days = balance.BalanceDays;

      context.LeaveLedger.Add(LeaveLedgerEntry.Lapse(employeeId, type.Id, year, days, yearEnd,
        type.CarryForwardAllowed ? $"Closed: carried into {year + 1}" : "Lapsed at the end of the year"));

      if (type.CarryForwardAllowed)
      {
        if (!hasNext.Contains(key))
          context.LeaveEntitlements.Add(LeaveEntitlement.Placeholder(LeaveEntitlementId.New(), employeeId, type, year + 1));
        context.LeaveLedger.Add(LeaveLedgerEntry.CarryForward(employeeId, type.Id, year + 1, days, yearEnd.AddDays(1), $"Brought forward from {year}"));
        carried++;
        daysCarried += days;
      }
      else
      {
        lapsed++;
        daysLapsed += days;
      }
    }

    if (!command.DryRun)
    {
      using (audit.Quietly())
      {
        var where = scope.IsLimited ? $" · offices: {await scope.OfficeNamesAsync(cancellationToken)}" : "";
        audit.Record(AuditActions.Ran, HrmRuns.LeaveYearEnd, null, year.ToString(),
          detail: $"{year} closed · {carried} balance(s) carried ({daysCarried:0.##} days) · {lapsed} lapsed ({daysLapsed:0.##} days){where}");
        await context.SaveChangesAsync(cancellationToken);
      }
    }

    return Result<LeaveYearEndResult>.Success(new(year, carried, lapsed, daysCarried, daysLapsed, command.DryRun, pending));
  }

  // ---- applications ----

  public async Task<Result<GetLeaveApplicationsQueryResult>> Handle(GetLeaveApplicationsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.LeaveApplications.AsNoTracking();
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(a => a.EmployeeId == employeeId);
    }
    if (query.Status is { } status)
      rows = rows.Where(a => a.Status == status);
    if (query.LeaveTypeId is { } type)
    {
      var typeId = LeaveTypeId.Of(type);
      rows = rows.Where(a => a.LeaveTypeId == typeId);
    }
    if (query.From is { } from)
      rows = rows.Where(a => a.EndDate >= from);
    if (query.To is { } to)
      rows = rows.Where(a => a.StartDate <= to);
    if (query.OrgUnitId is { } unit)
    {
      var inUnit = await lookup.EmployeesInUnitAsync(OrganizationUnitId.Of(unit), clock.Today, cancellationToken);
      rows = rows.Where(a => inUnit.Contains(a.EmployeeId));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(a => a.StartDate).ThenByDescending(a => a.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await MapAsync(page, cancellationToken);
    return Result<GetLeaveApplicationsQueryResult>.Success(new(new PaginatedResult<LeaveApplicationDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetLeaveApplicationQueryResult>> Handle(GetLeaveApplicationQuery query, CancellationToken cancellationToken)
  {
    var application = await context.LoadLeaveApplicationAsync(query.Id, cancellationToken);
    return Result<GetLeaveApplicationQueryResult>.Success(new((await MapAsync([application], cancellationToken))[0]));
  }

  public async Task<Result<LeaveDayCountResult>> Handle(CountLeaveDaysQuery query, CancellationToken cancellationToken)
  {
    var days = await leave.CountAsync(EmployeeId.Of(query.EmployeeId), query.StartDate, query.EndDate, cancellationToken);
    return Result<LeaveDayCountResult>.Success(new(days.Sum(d => d.Days), days));
  }

  public async Task<Result<CreatedResult>> Handle(ApplyLeaveCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var type = await context.LoadLeaveTypeAsync(command.Leave.LeaveTypeId, cancellationToken);
    var input = command.Leave;

    var days = await leave.DaysAsync(employee.Id, input.StartDate, input.EndDate, input.Days, cancellationToken);
    var others = await LiveApplicationsAsync(employee.Id, cancellationToken);
    var paper = input.SupportingDocumentId is { } paperId ? await context.LoadDocumentAsync(paperId, cancellationToken) : null;
    var application = LeaveApplication.Apply(LeaveApplicationId.New(), employee, type, input.StartDate, input.EndDate, days.Sum(d => d.Days),
      input.Reason, clock.Today, others, paper);
    await leave.EnsureAvailableAsync(employee.Id, type, days, null, cancellationToken);

    context.LeaveApplications.Add(application);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(application.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeLeaveApplicationCommand command, CancellationToken cancellationToken)
  {
    var application = await context.LoadLeaveApplicationAsync(command.Id, cancellationToken);
    EnsureOwn(application, command.OnlyForEmployee);
    if (LeaveTypeId.Of(command.Leave.LeaveTypeId) != application.LeaveTypeId)
      return Result<UpdatedResult>.Failure("The leave type of an application cannot change; cancel it and apply again.");

    var type = await context.LeaveTypes.FirstAsync(t => t.Id == application.LeaveTypeId, cancellationToken);
    var input = command.Leave;
    var days = await leave.DaysAsync(application.EmployeeId, input.StartDate, input.EndDate, input.Days, cancellationToken);
    var paper = input.SupportingDocumentId is { } paperId ? await context.LoadDocumentAsync(paperId, cancellationToken) : null;
    var employee = paper is null ? null : await context.LoadEmployeeAsync(application.EmployeeId.Value, cancellationToken);
    application.Change(input.StartDate, input.EndDate, days.Sum(d => d.Days), input.Reason, await LiveApplicationsAsync(application.EmployeeId, cancellationToken), employee, paper);
    await leave.EnsureAvailableAsync(application.EmployeeId, type, days, application.Id, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DecideLeaveCommand command, CancellationToken cancellationToken)
  {
    var application = await context.LoadLeaveApplicationAsync(command.Id, cancellationToken);
    EnsureOwn(application, command.OnlyForEmployee);
    var today = clock.Today;
    var now = clock.UtcNow;
    if (command.Decision != LeaveDecision.Cancel)
    {
      // nobody decides their own leave (OWN_RECORD); leave a clerk entered is decided by someone else (LEAVE_APPROVE)
      await duty.EnsureNotOwnRecordAsync(application.EmployeeId, "decide leave", application, cancellationToken);
      await duty.EnsureSeparateAsync(DutyRules.LeaveApprove, application.CreatedBy, "decide leave you entered", application, cancellationToken);
    }

    switch (command.Decision)
    {
      case LeaveDecision.Approve:
      {
        var type = await context.LeaveTypes.FirstAsync(t => t.Id == application.LeaveTypeId, cancellationToken);
        // the applied days stand (the calendar may have changed since); they are spread over the years they fall in
        var counted = await leave.CountAsync(application.EmployeeId, application.StartDate, application.EndDate, cancellationToken);
        var days = LeaveDayCounter.Split(application.Days, counted, application.StartDate);
        await leave.EnsureAvailableAsync(application.EmployeeId, type, days, application.Id, cancellationToken);
        // the own-record check is the duty rule's (above): the domain is not asked again
        foreach (var entry in application.Approve(currentUser.UserId, null, now, days))
          context.LeaveLedger.Add(entry);
        audit.Record(HrmVerbs.Approved, application, $"{application.Days:0.##} day(s) {application.StartDate:yyyy-MM-dd} to {application.EndDate:yyyy-MM-dd}");

        // days already closed as absent become leave
        foreach (var record in await ClosedDaysAsync(application, AttendanceStatus.Absent, cancellationToken))
          record.MarkOnLeave(type.Name);
        break;
      }

      case LeaveDecision.Reject:
        application.Reject(currentUser.UserId, null, now, command.Reason);
        audit.Record(HrmVerbs.Rejected, application, $"Reason: {command.Reason}");
        break;

      case LeaveDecision.Cancel:
        var wasApproved = application.Status == LeaveStatus.Approved;
        var reason = command.Reason;
        if (command.OnlyForEmployee is not null)
        {
          if (wasApproved && application.StartDate <= today)
            return Result<UpdatedResult>.Failure("The leave has started; ask HR to cancel it.");
          reason ??= "Withdrawn by the employee.";
        }
        else if (wasApproved && !currentUser.HasPermission(PermissionCatalog.Attendance.Approve))
        {
          // an approval is undone only by someone who may approve: the days already taken come back to the balance
          return Result<UpdatedResult>.Failure("Cancelling an approved leave needs the right to approve leave. Ask a leave approver.");
        }

        var usage = await context.LeaveLedger.Where(l => l.LeaveApplicationId == application.Id).ToListAsync(cancellationToken);
        foreach (var entry in application.Cancel(usage, currentUser.UserId, now, reason))
          context.LeaveLedger.Add(entry);
        audit.Record(HrmVerbs.Cancelled, application, reason is null ? null : $"Reason: {reason}");

        // days already closed as leave become absences again
        if (wasApproved)
          foreach (var record in await ClosedDaysAsync(application, AttendanceStatus.OnLeave, cancellationToken))
            record.ClearLeave();
        break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private Task<List<AttendanceRecord>> ClosedDaysAsync(LeaveApplication application, AttendanceStatus status, CancellationToken cancellationToken) =>
      context.AttendanceRecords.Where(r => r.EmployeeId == application.EmployeeId && r.Status == status && r.CheckIn == null
        && r.AttendanceDate >= application.StartDate && r.AttendanceDate <= application.EndDate).ToListAsync(cancellationToken);

  private static void EnsureOwn(LeaveApplication application, Guid? onlyFor)
  {
    if (onlyFor is { } employee && application.EmployeeId.Value != employee)
      throw new LeaveApplicationNotFoundException($"Leave application {application.Id.Value} was not found.");
  }

  private Task<List<LeaveApplication>> LiveApplicationsAsync(EmployeeId employeeId, CancellationToken cancellationToken) =>
      context.LeaveApplications.AsNoTracking()
        .Where(a => a.EmployeeId == employeeId && (a.Status == LeaveStatus.Pending || a.Status == LeaveStatus.Approved))
        .ToListAsync(cancellationToken);

  private async Task<Dictionary<LeaveTypeId, string>> TypeNamesAsync(CancellationToken cancellationToken) =>
      await context.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

  private async Task<List<LeaveApplicationDto>> MapAsync(List<LeaveApplication> rows, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(rows.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);
    var types = await TypeNamesAsync(cancellationToken);
    var paperIds = rows.Where(a => a.SupportingDocumentId is not null).Select(a => a.SupportingDocumentId!).ToList();
    var papers = paperIds.Count == 0 ? [] : await (
        from d in context.Documents.AsNoTracking()
        where paperIds.Contains(d.Id)
        join t in context.DocumentTypes on d.DocumentTypeId equals t.Id
        select new { d.Id, t.Name, d.DocumentNumber })
      .ToDictionaryAsync(d => d.Id, d => d.DocumentNumber is null ? d.Name : $"{d.Name} {d.DocumentNumber}", cancellationToken);
    return rows.Select(a => new LeaveApplicationDto(a.Id.Value, a.EmployeeId.Value, people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
      people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", a.LeaveTypeId.Value, types.GetValueOrDefault(a.LeaveTypeId), a.StartDate, a.EndDate,
      a.Days, a.Status, a.AppliedDate, a.ApprovedBy, a.ApprovalDate, a.Reason, a.CreatedAt, a.UpdatedBy, a.DecidedBy, a.DecidedAt, a.DecisionReason,
      a.HrActionId?.Value, CreatedBy: a.CreatedBy, SupportingDocumentId: a.SupportingDocumentId?.Value,
      SupportingDocument: a.SupportingDocumentId is null ? null : papers.GetValueOrDefault(a.SupportingDocumentId))).ToList();
  }
}
