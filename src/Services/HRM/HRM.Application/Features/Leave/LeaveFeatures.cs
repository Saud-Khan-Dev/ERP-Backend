using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- leave types ----

public sealed record LeaveTypeInput(string Name, bool IsPaid, decimal? MaxDaysPerYear, string? AccrualRule, bool CarryForwardAllowed, bool AffectsPayroll);

public sealed record GetLeaveTypesQueryResult(IReadOnlyList<LeaveTypeDto> LeaveTypes);
public sealed record GetLeaveTypesQuery : IQuery<Result<GetLeaveTypesQueryResult>>;
public sealed record CreateLeaveTypeCommand(LeaveTypeInput Type) : ICommand<Result<CreatedResult>>;
public sealed record UpdateLeaveTypeCommand(Guid Id, LeaveTypeInput Type) : ICommand<Result<UpdatedResult>>;

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

public sealed record LeaveYearEndResult(int Year, int CarriedForward, int Lapsed, decimal DaysCarried, decimal DaysLapsed, bool DryRun);

/// Closes a leave year: what is left of each balance is carried into the next year (types that allow it) or lapses.
/// Balances already closed are skipped, so it can be run again safely.
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

public sealed record LeaveApplicationInput(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, decimal? Days, string? Reason);

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
public sealed record DecideLeaveCommand(Guid Id, LeaveDecision Decision, Guid? OnlyForEmployee = null) : ICommand<Result<UpdatedResult>>;

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

public class LeaveHandlers(IApplicationDbContext context, LeaveService leave, HrLookup lookup, ICurrentUser currentUser, IClock clock) :
  IQueryHandler<GetLeaveTypesQuery, Result<GetLeaveTypesQueryResult>>,
  ICommandHandler<CreateLeaveTypeCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateLeaveTypeCommand, Result<UpdatedResult>>,
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
    var type = LeaveType.Create(LeaveTypeId.New(), i.Name, i.IsPaid, i.MaxDaysPerYear, i.AccrualRule, i.CarryForwardAllowed, i.AffectsPayroll);
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

    type.Update(i.Name, i.IsPaid, i.MaxDaysPerYear, i.AccrualRule, i.CarryForwardAllowed, i.AffectsPayroll);
    if (await context.LeaveTypes.AnyAsync(t => t.Id != type.Id && t.Name.ToLower() == type.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another leave type is named '{type.Name}'.");

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

    var employees = context.Employees.AsNoTracking().Where(e => e.ProfileStatus == RecordStatus.Active
      && e.EmploymentStatus != EmploymentStatus.Retired && e.EmploymentStatus != EmploymentStatus.Resigned
      && e.EmploymentStatus != EmploymentStatus.Terminated && e.EmploymentStatus != EmploymentStatus.Deceased);
    if (command.EmployeeIds is { Count: > 0 })
    {
      var only = command.EmployeeIds.Select(EmployeeId.Of).ToList();
      employees = employees.Where(e => only.Contains(e.Id));
    }

    var ids = await employees.Select(e => e.Id).ToListAsync(cancellationToken);
    var have = (await context.LeaveEntitlements.Where(e => ids.Contains(e.EmployeeId) && e.LeaveTypeId == type.Id && e.Year == command.Year)
      .Select(e => e.EmployeeId).ToListAsync(cancellationToken)).ToHashSet();

    foreach (var id in ids.Where(id => !have.Contains(id)))
      context.LeaveEntitlements.Add(LeaveEntitlement.Create(LeaveEntitlementId.New(), id, type, command.Year, days));

    await context.SaveChangesAsync(cancellationToken);
    return Result<GrantEntitlementsResult>.Success(new(ids.Count - have.Count, have.Count));
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
    var type = await context.LoadLeaveTypeAsync(command.LeaveTypeId, cancellationToken);
    var date = command.TransactionDate ?? clock.Today;

    var entry = command.TransactionType switch
    {
      LeaveTransactionType.Opening => LeaveLedgerEntry.Opening(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks),
      LeaveTransactionType.Accrual => LeaveLedgerEntry.Accrual(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks),
      LeaveTransactionType.Encashment => LeaveLedgerEntry.Encashment(employee.Id, type.Id, command.Year, Math.Abs(command.Days), date, command.Remarks),
      _ => LeaveLedgerEntry.Adjustment(employee.Id, type.Id, command.Year, command.Days, date, command.Remarks)
    };

    // the balance a ledger row belongs to needs its entitlement row (balances are read from entitlements)
    if (!await context.LeaveEntitlements.AnyAsync(e => e.EmployeeId == employee.Id && e.LeaveTypeId == type.Id && e.Year == command.Year, cancellationToken))
      context.LeaveEntitlements.Add(LeaveEntitlement.Create(LeaveEntitlementId.New(), employee.Id, type, command.Year, 0));

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
        type.CarryForwardAllowed ? $"Carried into {year + 1}" : "Lapsed at the end of the year"));

      if (type.CarryForwardAllowed)
      {
        if (!hasNext.Contains(key))
          context.LeaveEntitlements.Add(LeaveEntitlement.Create(LeaveEntitlementId.New(), employeeId, type, year + 1, 0));
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
      await context.SaveChangesAsync(cancellationToken);

    return Result<LeaveYearEndResult>.Success(new(year, carried, lapsed, daysCarried, daysLapsed, command.DryRun));
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
    var days = await leave.DaysAsync(EmployeeId.Of(query.EmployeeId), query.StartDate, query.EndDate, null, cancellationToken);
    return Result<LeaveDayCountResult>.Success(new(days.Sum(d => d.Days), days));
  }

  public async Task<Result<CreatedResult>> Handle(ApplyLeaveCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var type = await context.LoadLeaveTypeAsync(command.Leave.LeaveTypeId, cancellationToken);
    var input = command.Leave;

    var days = await leave.DaysAsync(employee.Id, input.StartDate, input.EndDate, input.Days, cancellationToken);
    var others = await LiveApplicationsAsync(employee.Id, cancellationToken);
    var application = LeaveApplication.Apply(LeaveApplicationId.New(), employee, type, input.StartDate, input.EndDate, days.Sum(d => d.Days),
      input.Reason, clock.Today, others);
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
    application.Change(input.StartDate, input.EndDate, days.Sum(d => d.Days), input.Reason, await LiveApplicationsAsync(application.EmployeeId, cancellationToken));
    await leave.EnsureAvailableAsync(application.EmployeeId, type, days, application.Id, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DecideLeaveCommand command, CancellationToken cancellationToken)
  {
    var application = await context.LoadLeaveApplicationAsync(command.Id, cancellationToken);
    EnsureOwn(application, command.OnlyForEmployee);
    var today = clock.Today;

    switch (command.Decision)
    {
      case LeaveDecision.Approve:
        var type = await context.LeaveTypes.FirstAsync(t => t.Id == application.LeaveTypeId, cancellationToken);
        var days = await leave.DaysAsync(application.EmployeeId, application.StartDate, application.EndDate, application.Days, cancellationToken);
        await leave.EnsureAvailableAsync(application.EmployeeId, type, days, application.Id, cancellationToken);
        foreach (var entry in application.Approve(currentUser.UserId, today, days))
          context.LeaveLedger.Add(entry);

        // days already closed as absent become leave
        foreach (var record in await ClosedDaysAsync(application, AttendanceStatus.Absent, cancellationToken))
          record.MarkOnLeave(type.Name);
        break;

      case LeaveDecision.Reject:
        application.Reject();
        break;

      case LeaveDecision.Cancel:
        if (command.OnlyForEmployee is not null && application.Status == LeaveStatus.Approved && application.StartDate <= today)
          return Result<UpdatedResult>.Failure("The leave has started; ask HR to cancel it.");
        var wasApproved = application.Status == LeaveStatus.Approved;
        var usage = await context.LeaveLedger.Where(l => l.LeaveApplicationId == application.Id).ToListAsync(cancellationToken);
        foreach (var entry in application.Cancel(usage, today))
          context.LeaveLedger.Add(entry);

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
    return rows.Select(a => new LeaveApplicationDto(a.Id.Value, a.EmployeeId.Value, people.GetValueOrDefault(a.EmployeeId.Value)?.EmployeeNumber ?? "",
      people.GetValueOrDefault(a.EmployeeId.Value)?.FullName ?? "", a.LeaveTypeId.Value, types.GetValueOrDefault(a.LeaveTypeId), a.StartDate, a.EndDate,
      a.Days, a.Status, a.AppliedDate, a.ApprovedBy, a.ApprovalDate, a.Reason, a.CreatedAt, a.UpdatedBy)).ToList();
  }
}
