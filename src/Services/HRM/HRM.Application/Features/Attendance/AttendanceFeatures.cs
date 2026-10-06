using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- shifts ----

public sealed record WorkShiftInput(string Name, TimeOnly StartTime, TimeOnly EndTime, int GraceMinutes, IReadOnlyList<int>? WorkingWeekdays);

public sealed record GetWorkShiftsQueryResult(IReadOnlyList<WorkShiftDto> Shifts);
public sealed record GetWorkShiftsQuery(bool IncludeInactive) : IQuery<Result<GetWorkShiftsQueryResult>>;
public sealed record CreateWorkShiftCommand(WorkShiftInput Shift) : ICommand<Result<CreatedResult>>;
public sealed record UpdateWorkShiftCommand(Guid Id, WorkShiftInput Shift) : ICommand<Result<UpdatedResult>>;
public sealed record SetWorkShiftActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public sealed record GetEmployeeShiftsQueryResult(IReadOnlyList<EmployeeShiftDto> Shifts);
public sealed record GetEmployeeShiftsQuery(Guid EmployeeId) : IQuery<Result<GetEmployeeShiftsQueryResult>>;

/// Puts the employee on a shift from a date; the open shift assignment closes the day before.
public sealed record AssignShiftCommand(Guid EmployeeId, Guid WorkShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : ICommand<Result<CreatedResult>>;

/// Puts many employees (e.g. a whole unit) on a shift from a date.
public sealed record AssignShiftToManyCommand(Guid WorkShiftId, DateOnly EffectiveFrom, IReadOnlyList<Guid> EmployeeIds) : ICommand<Result<AssignShiftToManyResult>>;
public sealed record AssignShiftToManyResult(int Assigned, IReadOnlyList<string> Problems);

public sealed record EndEmployeeShiftCommand(Guid Id, DateOnly LastDay) : ICommand<Result<UpdatedResult>>;

public class WorkShiftInputValidator : AbstractValidator<WorkShiftInput>
{
  public WorkShiftInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.GraceMinutes).InclusiveBetween(0, 240);
    RuleForEach(x => x.WorkingWeekdays).InclusiveBetween(1, 7);
  }
}

public class CreateWorkShiftCommandValidator : AbstractValidator<CreateWorkShiftCommand>
{
  public CreateWorkShiftCommandValidator() => RuleFor(x => x.Shift).NotNull().SetValidator(new WorkShiftInputValidator());
}

public class UpdateWorkShiftCommandValidator : AbstractValidator<UpdateWorkShiftCommand>
{
  public UpdateWorkShiftCommandValidator() => RuleFor(x => x.Shift).NotNull().SetValidator(new WorkShiftInputValidator());
}

public class AssignShiftToManyCommandValidator : AbstractValidator<AssignShiftToManyCommand>
{
  public AssignShiftToManyCommandValidator()
  {
    RuleFor(x => x.WorkShiftId).NotEmpty();
    RuleFor(x => x.EmployeeIds).NotEmpty().Must(ids => ids.Count <= 2000).WithMessage("At most 2000 employees at a time.");
  }
}

public class ShiftHandlers(IApplicationDbContext context) :
  IQueryHandler<GetWorkShiftsQuery, Result<GetWorkShiftsQueryResult>>,
  ICommandHandler<CreateWorkShiftCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateWorkShiftCommand, Result<UpdatedResult>>,
  ICommandHandler<SetWorkShiftActivationCommand, Result<UpdatedResult>>,
  IQueryHandler<GetEmployeeShiftsQuery, Result<GetEmployeeShiftsQueryResult>>,
  ICommandHandler<AssignShiftCommand, Result<CreatedResult>>,
  ICommandHandler<AssignShiftToManyCommand, Result<AssignShiftToManyResult>>,
  ICommandHandler<EndEmployeeShiftCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetWorkShiftsQueryResult>> Handle(GetWorkShiftsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.WorkShifts.AsNoTracking().Where(s => query.IncludeInactive || s.IsActive).OrderBy(s => s.StartTime).ThenBy(s => s.Name).ToListAsync(cancellationToken);
    return Result<GetWorkShiftsQueryResult>.Success(new(rows.Select(s => s.ToDto()).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateWorkShiftCommand command, CancellationToken cancellationToken)
  {
    var input = command.Shift;
    var shift = WorkShift.Create(WorkShiftId.New(), input.Name, input.StartTime, input.EndTime, input.GraceMinutes, input.WorkingWeekdays);
    if (await context.WorkShifts.AnyAsync(s => s.Name.ToLower() == shift.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A shift named '{shift.Name}' already exists.");

    context.WorkShifts.Add(shift);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(shift.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateWorkShiftCommand command, CancellationToken cancellationToken)
  {
    var shift = await context.LoadShiftAsync(command.Id, cancellationToken);
    var input = command.Shift;
    shift.Update(input.Name, input.StartTime, input.EndTime, input.GraceMinutes, input.WorkingWeekdays);
    if (await context.WorkShifts.AnyAsync(s => s.Id != shift.Id && s.Name.ToLower() == shift.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another shift is named '{shift.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetWorkShiftActivationCommand command, CancellationToken cancellationToken)
  {
    var shift = await context.LoadShiftAsync(command.Id, cancellationToken);
    shift.SetActive(command.IsActive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetEmployeeShiftsQueryResult>> Handle(GetEmployeeShiftsQuery query, CancellationToken cancellationToken)
  {
    var employeeId = EmployeeId.Of(query.EmployeeId);
    var rows = await context.EmployeeShifts.AsNoTracking().Where(s => s.EmployeeId == employeeId).OrderByDescending(s => s.EffectiveFrom).ToListAsync(cancellationToken);
    var names = await context.WorkShifts.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    return Result<GetEmployeeShiftsQueryResult>.Success(new(rows.Select(s =>
      new EmployeeShiftDto(s.Id.Value, s.EmployeeId.Value, s.WorkShiftId.Value, names.GetValueOrDefault(s.WorkShiftId), s.EffectiveFrom, s.EffectiveTo)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(AssignShiftCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var shift = await context.LoadShiftAsync(command.WorkShiftId, cancellationToken);
    var existing = await context.EmployeeShifts.Where(s => s.EmployeeId == employee.Id).ToListAsync(cancellationToken);

    var assignment = EmployeeShift.Assign(EmployeeShiftId.New(), employee, shift, command.EffectiveFrom, command.EffectiveTo, existing);
    context.EmployeeShifts.Add(assignment);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(assignment.Id);
  }

  public async Task<Result<AssignShiftToManyResult>> Handle(AssignShiftToManyCommand command, CancellationToken cancellationToken)
  {
    var shift = await context.LoadShiftAsync(command.WorkShiftId, cancellationToken);
    var ids = command.EmployeeIds.Distinct().Select(EmployeeId.Of).ToList();
    var employees = await context.Employees.Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);
    var existing = (await context.EmployeeShifts.Where(s => ids.Contains(s.EmployeeId)).ToListAsync(cancellationToken)).ToLookup(s => s.EmployeeId);

    var problems = new List<string>();
    var assigned = 0;
    foreach (var employee in employees)
    {
      try
      {
        context.EmployeeShifts.Add(EmployeeShift.Assign(EmployeeShiftId.New(), employee, shift, command.EffectiveFrom, null, existing[employee.Id].ToList()));
        assigned++;
      }
      catch (DomainException error)
      {
        problems.Add($"{employee.EmployeeNumber}: {DomainMessages.Text(error)}");
      }
    }

    foreach (var missing in ids.Except(employees.Select(e => e.Id)))
      problems.Add($"{missing.Value}: no such employee.");

    await context.SaveChangesAsync(cancellationToken);
    return Result<AssignShiftToManyResult>.Success(new(assigned, problems));
  }

  public async Task<Result<UpdatedResult>> Handle(EndEmployeeShiftCommand command, CancellationToken cancellationToken)
  {
    var id = EmployeeShiftId.Of(command.Id);
    var shift = await context.EmployeeShifts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
      ?? throw new WorkShiftNotFoundException($"Shift assignment {command.Id} was not found.");
    shift.EndOn(command.LastDay);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }
}

// ---- holidays ----

public sealed record HolidayInput(DateOnly HolidayDate, string Name, HolidayType HolidayType, Guid? LocationId, string? NotificationRef);

public sealed record GetHolidaysQueryResult(IReadOnlyList<HolidayDto> Holidays);

/// A year's calendar (or a date range); LocationId adds that location's local holidays to the gazetted ones.
public sealed record GetHolidaysQuery(int? Year, DateOnly? From, DateOnly? To, Guid? LocationId) : IQuery<Result<GetHolidaysQueryResult>>;

public sealed record CreateHolidayCommand(HolidayInput Holiday) : ICommand<Result<CreatedResult>>;
public sealed record UpdateHolidayCommand(Guid Id, HolidayInput Holiday) : ICommand<Result<UpdatedResult>>;
public sealed record DeleteHolidayCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class HolidayInputValidator : AbstractValidator<HolidayInput>
{
  public HolidayInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.HolidayType).IsInEnum();
    RuleFor(x => x.NotificationRef).MaximumLength(200);
  }
}

public class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
  public CreateHolidayCommandValidator() => RuleFor(x => x.Holiday).NotNull().SetValidator(new HolidayInputValidator());
}

public class UpdateHolidayCommandValidator : AbstractValidator<UpdateHolidayCommand>
{
  public UpdateHolidayCommandValidator() => RuleFor(x => x.Holiday).NotNull().SetValidator(new HolidayInputValidator());
}

public class HolidayHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetHolidaysQuery, Result<GetHolidaysQueryResult>>,
  ICommandHandler<CreateHolidayCommand, Result<CreatedResult>>,
  ICommandHandler<UpdateHolidayCommand, Result<UpdatedResult>>,
  ICommandHandler<DeleteHolidayCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetHolidaysQueryResult>> Handle(GetHolidaysQuery query, CancellationToken cancellationToken)
  {
    var year = query.Year ?? clock.Today.Year;
    var from = query.From ?? new DateOnly(year, 1, 1);
    var to = query.To ?? new DateOnly(year, 12, 31);
    var holidays = context.Holidays.AsNoTracking().Where(h => h.HolidayDate >= from && h.HolidayDate <= to);
    if (query.LocationId is { } location)
    {
      var locationId = LocationId.Of(location);
      holidays = holidays.Where(h => h.LocationId == null || h.LocationId == locationId);
    }

    var rows = await holidays.OrderBy(h => h.HolidayDate).ToListAsync(cancellationToken);
    var locations = await lookup.LocationsAsync(rows.Select(h => h.LocationId), cancellationToken);
    return Result<GetHolidaysQueryResult>.Success(new(rows.Select(h => new HolidayDto(h.Id.Value, h.HolidayDate, h.Name, h.HolidayType,
      h.LocationId?.Value, h.LocationId is null ? null : locations.GetValueOrDefault(h.LocationId.Value), h.NotificationRef)).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreateHolidayCommand command, CancellationToken cancellationToken)
  {
    var input = command.Holiday;
    var locationId = await LocationAsync(input.LocationId, cancellationToken);
    var holiday = Holiday.Create(HolidayId.New(), input.HolidayDate, input.Name, input.HolidayType, locationId, input.NotificationRef);
    if (await context.Holidays.AnyAsync(h => h.HolidayDate == holiday.HolidayDate && h.Name.ToLower() == holiday.Name.ToLower() && h.LocationId == holiday.LocationId, cancellationToken))
      return Result<CreatedResult>.Failure($"'{holiday.Name}' is already on the calendar on {holiday.HolidayDate:yyyy-MM-dd} for that place.");

    context.Holidays.Add(holiday);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(holiday.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdateHolidayCommand command, CancellationToken cancellationToken)
  {
    var holiday = await LoadAsync(command.Id, cancellationToken);
    var input = command.Holiday;
    holiday.Update(input.HolidayDate, input.Name, input.HolidayType, await LocationAsync(input.LocationId, cancellationToken), input.NotificationRef);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteHolidayCommand command, CancellationToken cancellationToken)
  {
    var holiday = await LoadAsync(command.Id, cancellationToken);
    context.Holidays.Remove(holiday);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  private async Task<Holiday> LoadAsync(Guid id, CancellationToken cancellationToken)
  {
    var holidayId = HolidayId.Of(id);
    return await context.Holidays.FirstOrDefaultAsync(h => h.Id == holidayId, cancellationToken)
      ?? throw new HolidayNotFoundException($"Holiday {id} was not found.");
  }

  private async Task<LocationId?> LocationAsync(Guid? id, CancellationToken cancellationToken)
  {
    if (id is not { } value)
      return null;
    var location = await context.LoadLocationAsync(value, cancellationToken);
    location.EnsureActive();
    return location.Id;
  }
}

// ---- attendance ----

public sealed record AttendanceInput(DateOnly AttendanceDate, TimeOnly? CheckIn, TimeOnly? CheckOut, AttendanceStatus? Status, string? Remarks);

public sealed record GetAttendanceQueryResult(PaginatedResult<AttendanceRecordDto> Attendance);

public sealed record GetAttendanceQuery(
  PaginationRequest Pagination,
  DateOnly From,
  DateOnly To,
  Guid? EmployeeId,
  Guid? OrgUnitId,
  AttendanceStatus? Status) : IQuery<Result<GetAttendanceQueryResult>>;

public sealed record GetAttendanceSummaryQueryResult(int Year, int Month, IReadOnlyList<AttendanceSummaryDto> Summary);

public sealed record GetAttendanceSummaryQuery(int Year, int Month, Guid? EmployeeId, Guid? OrgUnitId) : IQuery<Result<GetAttendanceSummaryQueryResult>>;

/// Records (or corrects) one employee's day. Hours, lateness and the status are worked out from the shift.
public sealed record RecordAttendanceCommand(Guid EmployeeId, AttendanceInput Attendance) : ICommand<Result<CreatedResult>>;

public sealed record AttendanceImportRow(string EmployeeNumber, DateOnly AttendanceDate, TimeOnly? CheckIn, TimeOnly? CheckOut, AttendanceStatus? Status, string? Remarks);

public sealed record ImportAttendanceResult(int Saved, IReadOnlyList<string> Problems);

/// A batch from the biometric device or a sheet, by employee number. Every good row is saved; bad rows are reported.
public sealed record ImportAttendanceCommand(IReadOnlyList<AttendanceImportRow> Rows) : ICommand<Result<ImportAttendanceResult>>;

public sealed record CloseAttendanceDayResult(DateOnly Date, int Absent, int OnLeave, int Holiday, int Weekend, bool DryRun);

/// Closes a day: everyone holding a post that day without a record gets one - on leave (approved leave), holiday,
/// weekend (not a working day of their shift) or absent.
public sealed record CloseAttendanceDayCommand(DateOnly Date, bool DryRun) : ICommand<Result<CloseAttendanceDayResult>>;

public sealed record DeleteAttendanceCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class RecordAttendanceCommandValidator : AbstractValidator<RecordAttendanceCommand>
{
  public RecordAttendanceCommandValidator()
  {
    RuleFor(x => x.EmployeeId).NotEmpty();
    RuleFor(x => x.Attendance).NotNull();
    RuleFor(x => x.Attendance.Status).IsInEnum().When(x => x.Attendance?.Status is not null);
    RuleFor(x => x.Attendance.Remarks).MaximumLength(2000);
  }
}

public class ImportAttendanceCommandValidator : AbstractValidator<ImportAttendanceCommand>
{
  public ImportAttendanceCommandValidator() =>
    RuleFor(x => x.Rows).NotEmpty().Must(r => r.Count <= 20000).WithMessage("At most 20,000 rows per import.");
}

public class GetAttendanceQueryValidator : AbstractValidator<GetAttendanceQuery>
{
  public GetAttendanceQueryValidator()
  {
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 500);
    RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' cannot be before 'from'.");
    RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 366).WithMessage("Ask for at most a year at a time.");
  }
}

public class AttendanceHandlers(IApplicationDbContext context, WorkCalendar calendar, HrLookup lookup, IClock clock) :
  IQueryHandler<GetAttendanceQuery, Result<GetAttendanceQueryResult>>,
  IQueryHandler<GetAttendanceSummaryQuery, Result<GetAttendanceSummaryQueryResult>>,
  ICommandHandler<RecordAttendanceCommand, Result<CreatedResult>>,
  ICommandHandler<ImportAttendanceCommand, Result<ImportAttendanceResult>>,
  ICommandHandler<CloseAttendanceDayCommand, Result<CloseAttendanceDayResult>>,
  ICommandHandler<DeleteAttendanceCommand, Result<UpdatedResult>>
{
  public async Task<Result<GetAttendanceQueryResult>> Handle(GetAttendanceQuery query, CancellationToken cancellationToken)
  {
    var records = await FilterAsync(query.From, query.To, query.EmployeeId, query.OrgUnitId, cancellationToken);
    if (query.Status is { } status)
      records = records.Where(r => r.Status == status);

    var total = await records.LongCountAsync(cancellationToken);
    var page = await records.OrderByDescending(r => r.AttendanceDate).ThenBy(r => r.EmployeeId)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);

    var people = await lookup.EmployeesAsync(page.Select(r => (EmployeeId?)r.EmployeeId), cancellationToken);
    var shifts = await context.WorkShifts.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
    var data = page.Select(r => new AttendanceRecordDto(r.Id.Value, r.EmployeeId.Value, people.GetValueOrDefault(r.EmployeeId.Value)?.EmployeeNumber ?? "",
      people.GetValueOrDefault(r.EmployeeId.Value)?.FullName ?? "", r.AttendanceDate, r.WorkShiftId?.Value,
      r.WorkShiftId is null ? null : shifts.GetValueOrDefault(r.WorkShiftId), r.CheckIn, r.CheckOut, r.WorkingHours, r.OvertimeHours,
      r.LateMinutes, r.EarlyDepartureMinutes, r.Status, r.Remarks)).ToList();

    return Result<GetAttendanceQueryResult>.Success(new(new PaginatedResult<AttendanceRecordDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetAttendanceSummaryQueryResult>> Handle(GetAttendanceSummaryQuery query, CancellationToken cancellationToken)
  {
    if (query.Month is < 1 or > 12)
      return Result<GetAttendanceSummaryQueryResult>.Failure("Month must be 1-12.");

    var from = new DateOnly(query.Year, query.Month, 1);
    var to = from.AddMonths(1).AddDays(-1);
    var records = await (await FilterAsync(from, to, query.EmployeeId, query.OrgUnitId, cancellationToken)).ToListAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(records.Select(r => (EmployeeId?)r.EmployeeId), cancellationToken);

    var summary = records.GroupBy(r => r.EmployeeId.Value).Select(g =>
    {
      int Count(AttendanceStatus status) => g.Count(r => r.Status == status);
      return new AttendanceSummaryDto(g.Key, people.GetValueOrDefault(g.Key)?.EmployeeNumber ?? "", people.GetValueOrDefault(g.Key)?.FullName ?? "",
        Count(AttendanceStatus.Present), Count(AttendanceStatus.Late), Count(AttendanceStatus.HalfDay), Count(AttendanceStatus.Absent),
        Count(AttendanceStatus.OnLeave), Count(AttendanceStatus.OfficialDuty), Count(AttendanceStatus.Holiday), Count(AttendanceStatus.Weekend),
        g.Sum(r => r.LateMinutes), g.Sum(r => r.WorkingHours ?? 0), g.Sum(r => r.OvertimeHours ?? 0));
    }).OrderBy(s => s.EmployeeNumber).ToList();

    return Result<GetAttendanceSummaryQueryResult>.Success(new(query.Year, query.Month, summary));
  }

  public async Task<Result<CreatedResult>> Handle(RecordAttendanceCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    employee.EnsureProfileActive();
    var record = await UpsertAsync(employee.Id, command.Attendance, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(record.Id);
  }

  public async Task<Result<ImportAttendanceResult>> Handle(ImportAttendanceCommand command, CancellationToken cancellationToken)
  {
    var numbers = command.Rows.Select(r => Employee.NormalizeNumber(r.EmployeeNumber)).Distinct().ToList();
    var employees = await context.Employees.AsNoTracking().Where(e => numbers.Contains(e.EmployeeNumber)).ToDictionaryAsync(e => e.EmployeeNumber, cancellationToken);

    var problems = new List<string>();
    var saved = 0;
    for (var i = 0; i < command.Rows.Count; i++)
    {
      var row = command.Rows[i];
      var number = Employee.NormalizeNumber(row.EmployeeNumber);
      if (!employees.TryGetValue(number, out var employee))
      {
        problems.Add($"Row {i + 1}: no employee {number}.");
        continue;
      }

      try
      {
        await UpsertAsync(employee.Id, new AttendanceInput(row.AttendanceDate, row.CheckIn, row.CheckOut, row.Status, row.Remarks), cancellationToken);
        saved++;
      }
      catch (DomainException error)
      {
        problems.Add($"Row {i + 1} ({number}, {row.AttendanceDate:yyyy-MM-dd}): {DomainMessages.Text(error)}");
      }
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<ImportAttendanceResult>.Success(new(saved, problems));
  }

  public async Task<Result<CloseAttendanceDayResult>> Handle(CloseAttendanceDayCommand command, CancellationToken cancellationToken)
  {
    var date = command.Date;
    AttendanceRecord.EnsureRecordable(date, clock.Today);

    // everyone holding a post that day without a record. Holding the post on the date is what counts, not today's
    // status: someone who retired or was deputed out later was still serving then (both end the assignment).
    var employeeIds = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .Select(a => a.EmployeeId).Distinct()
      .Where(id => !context.AttendanceRecords.Any(r => r.EmployeeId == id && r.AttendanceDate == date))
      .Where(id => context.Employees.Any(e => e.Id == id && e.ProfileStatus == RecordStatus.Active))
      .ToListAsync(cancellationToken);

    var onLeave = (await context.LeaveApplications.AsNoTracking()
        .Where(a => employeeIds.Contains(a.EmployeeId) && a.Status == LeaveStatus.Approved && a.StartDate <= date && a.EndDate >= date)
        .Select(a => a.EmployeeId).ToListAsync(cancellationToken))
      .ToHashSet();
    var calendars = await calendar.ForManyAsync(employeeIds, date, date, cancellationToken);

    int absent = 0, leave = 0, holiday = 0, weekend = 0;
    foreach (var id in employeeIds)
    {
      var employeeCalendar = calendars[id];
      var status = onLeave.Contains(id) ? AttendanceStatus.OnLeave
        : employeeCalendar.IsHoliday(date) ? AttendanceStatus.Holiday
        : !employeeCalendar.IsWorkingDay(date) ? AttendanceStatus.Weekend
        : AttendanceStatus.Absent;

      switch (status)
      {
        case AttendanceStatus.OnLeave: leave++; break;
        case AttendanceStatus.Holiday: holiday++; break;
        case AttendanceStatus.Weekend: weekend++; break;
        default: absent++; break;
      }

      context.AttendanceRecords.Add(AttendanceRecord.Record(AttendanceRecordId.New(), id, date, employeeCalendar.ShiftOn(date), null, null, status, "Recorded when the day was closed."));
    }

    if (!command.DryRun)
      await context.SaveChangesAsync(cancellationToken);

    return Result<CloseAttendanceDayResult>.Success(new(date, absent, leave, holiday, weekend, command.DryRun));
  }

  public async Task<Result<UpdatedResult>> Handle(DeleteAttendanceCommand command, CancellationToken cancellationToken)
  {
    var id = AttendanceRecordId.Of(command.Id);
    var record = await context.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new AttendanceRecordNotFoundException($"Attendance record {command.Id} was not found.");
    context.AttendanceRecords.Remove(record);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// One record per employee per day: an existing one is corrected in place.
  private async Task<AttendanceRecord> UpsertAsync(EmployeeId employeeId, AttendanceInput input, CancellationToken cancellationToken)
  {
    AttendanceRecord.EnsureRecordable(input.AttendanceDate, clock.Today);
    var shift = (await calendar.ForAsync(employeeId, input.AttendanceDate, input.AttendanceDate, cancellationToken)).ShiftOn(input.AttendanceDate);
    var existing = context.AttendanceRecords.Local.FirstOrDefault(r => r.EmployeeId == employeeId && r.AttendanceDate == input.AttendanceDate)
      ?? await context.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.AttendanceDate == input.AttendanceDate, cancellationToken);

    if (existing is not null)
    {
      existing.Update(shift, input.CheckIn, input.CheckOut, input.Status, input.Remarks);
      return existing;
    }

    var record = AttendanceRecord.Record(AttendanceRecordId.New(), employeeId, input.AttendanceDate, shift, input.CheckIn, input.CheckOut, input.Status, input.Remarks);
    context.AttendanceRecords.Add(record);
    return record;
  }

  private async Task<IQueryable<AttendanceRecord>> FilterAsync(DateOnly from, DateOnly to, Guid? employee, Guid? unit, CancellationToken cancellationToken)
  {
    var records = context.AttendanceRecords.AsNoTracking().Where(r => r.AttendanceDate >= from && r.AttendanceDate <= to);
    if (employee is { } e)
    {
      var employeeId = EmployeeId.Of(e);
      records = records.Where(r => r.EmployeeId == employeeId);
    }
    if (unit is { } u)
    {
      var units = (await lookup.SubtreeAsync(OrganizationUnitId.Of(u), to, cancellationToken)).ToList();
      records = records.Where(r => context.PositionAssignments.Any(a => a.EmployeeId == r.EmployeeId && a.AssignmentType == AssignmentType.Regular
        && a.Status == RecordStatus.Active && a.EffectiveFrom <= r.AttendanceDate && (a.EffectiveTo == null || a.EffectiveTo >= r.AttendanceDate)
        && context.PostVersions.Any(v => v.PostId == a.PostId && units.Contains(v.OrgUnitId)
          && v.EffectiveFrom <= r.AttendanceDate && (v.EffectiveTo == null || v.EffectiveTo >= r.AttendanceDate))));
    }
    return records;
  }
}
