public sealed record AssignShiftRequest(Guid WorkShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record AssignShiftToManyRequest(DateOnly EffectiveFrom, IReadOnlyList<Guid> EmployeeIds);
public sealed record EndShiftRequest(DateOnly LastDay);
public sealed record ImportAttendanceRequest(IReadOnlyList<AttendanceImportRow> Rows);
public sealed record CloseAttendanceDayRequest(DateOnly Date, bool DryRun);

/// Work shifts and who works which, holidays, and the daily attendance register (recorded, imported from the device,
/// and closed each day so that everyone holding a post has a row).
public class AttendanceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- work shifts ----

    var shifts = app.MapGroup("/work-shifts").WithTags("Work Shifts");

    shifts.MapGet("/", async (bool? includeInactive, ISender sender) => (await sender.Send(new GetWorkShiftsQuery(includeInactive ?? false))).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithName("GetWorkShifts")
      .Produces<GetWorkShiftsQueryResult>()
      .WithSummary("Get Work Shifts")
      .WithDescription("Active shifts only unless includeInactive=true. workingWeekdays are ISO weekdays (1 = Monday ... 7 = Sunday).");

    shifts.MapPost("/", async (WorkShiftInput shift, ISender sender) =>
        (await sender.Send(new CreateWorkShiftCommand(shift))).ToCreated(r => $"/work-shifts/{r.Id}"))
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithName("CreateWorkShift")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Work Shift")
      .WithDescription("An end time before the start time is an overnight shift. Working weekdays default to Monday-Friday.");

    shifts.MapPut("/{id:guid}", async (Guid id, WorkShiftInput shift, ISender sender) => (await sender.Send(new UpdateWorkShiftCommand(id, shift))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("UpdateWorkShift")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Work Shift")
      .WithDescription("Days already recorded keep the hours worked out when they were recorded.");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      shifts.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetWorkShiftActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.Attendance.Edit)
        .WithName(active ? "ActivateWorkShift" : "DeactivateWorkShift")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(active ? "Activate Work Shift" : "Deactivate Work Shift");
    }

    shifts.MapPost("/{id:guid}/assign", async (Guid id, AssignShiftToManyRequest request, ISender sender) =>
        (await sender.Send(new AssignShiftToManyCommand(id, request.EffectiveFrom, request.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("AssignShiftToMany")
      .Produces<AssignShiftToManyResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Put Employees on Shift")
      .WithDescription("Moves each employee onto the shift from the date (their current shift ends the day before). Employees that cannot be moved are listed in problems; the rest are saved.");

    app.MapGet("/employees/{id:guid}/shifts", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeeShiftsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithTags("Work Shifts")
      .WithName("GetEmployeeShifts")
      .Produces<GetEmployeeShiftsQueryResult>()
      .WithSummary("Get Employee Shifts")
      .WithDescription("The employee's shifts over time, newest first. Without a shift the working week is Monday-Friday.");

    app.MapPost("/employees/{id:guid}/shifts", async (Guid id, AssignShiftRequest request, ISender sender) =>
        (await sender.Send(new AssignShiftCommand(id, request.WorkShiftId, request.EffectiveFrom, request.EffectiveTo))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithTags("Work Shifts")
      .WithName("AssignShift")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Assign Shift")
      .WithDescription("The open-ended current shift ends the day before; a shift cannot overlap another.");

    app.MapPost("/employee-shifts/{id:guid}/end", async (Guid id, EndShiftRequest request, ISender sender) =>
        (await sender.Send(new EndEmployeeShiftCommand(id, request.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithTags("Work Shifts")
      .WithName("EndEmployeeShift")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("End Employee Shift");

    // ---- holidays ----

    var holidays = app.MapGroup("/holidays").WithTags("Holidays");

    holidays.MapGet("/", async (int? year, DateOnly? from, DateOnly? to, Guid? locationId, ISender sender) =>
        (await sender.Send(new GetHolidaysQuery(year, from, to, locationId))).ToOk())
      .RequireAuthorization()
      .WithName("GetHolidays")
      .Produces<GetHolidaysQueryResult>()
      .WithSummary("Get Holidays")
      .WithDescription("By year or date range. locationId: the gazetted holidays plus that location's local ones.");

    holidays.MapPost("/", async (HolidayInput holiday, ISender sender) =>
        (await sender.Send(new CreateHolidayCommand(holiday))).ToCreated(r => $"/holidays/{r.Id}"))
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithName("CreateHoliday")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Holiday")
      .WithDescription("holidayType = public | religious | provincial | optional. Without a location it applies everywhere; with one, only to staff working there.");

    holidays.MapPut("/{id:guid}", async (Guid id, HolidayInput holiday, ISender sender) => (await sender.Send(new UpdateHolidayCommand(id, holiday))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("UpdateHoliday")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Holiday");

    holidays.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteHolidayCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Delete)
      .WithName("DeleteHoliday")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Holiday");

    // ---- attendance ----

    var attendance = app.MapGroup("/attendance").WithTags("Attendance");

    attendance.MapGet("/", async (DateOnly from, DateOnly to, int? pageIndex, int? pageSize, Guid? employeeId, Guid? orgUnitId, string? status, ISender sender) =>
        (await sender.Send(new GetAttendanceQuery(QueryParsing.Page(pageIndex, pageSize, 50), from, to, employeeId, orgUnitId,
          QueryParsing.ParseEnum<AttendanceStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithName("GetAttendance")
      .Produces<GetAttendanceQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Attendance")
      .WithDescription("Days between from and to (at most a year), newest first. orgUnitId takes in its sub-units. "
        + "status = present | absent | late | half_day | on_leave | official_duty | holiday | weekend.");

    attendance.MapGet("/summary", async (int year, int month, Guid? employeeId, Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetAttendanceSummaryQuery(year, month, employeeId, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithName("GetAttendanceSummary")
      .Produces<GetAttendanceSummaryQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Monthly Attendance Summary")
      .WithDescription("Per employee: days by status, minutes late and hours worked in the month.");

    app.MapPost("/employees/{id:guid}/attendance", async (Guid id, AttendanceInput day, ISender sender) =>
        (await sender.Send(new RecordAttendanceCommand(id, day))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithTags("Attendance")
      .WithName("RecordAttendance")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Attendance")
      .WithDescription("Records or corrects the employee's day. Without a status it is worked out: absent without a check-in, half day under half the shift, late after the grace minutes, else present.");

    attendance.MapPost("/import", async (ImportAttendanceRequest request, ISender sender) =>
        (await sender.Send(new ImportAttendanceCommand(request.Rows))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithName("ImportAttendance")
      .Produces<ImportAttendanceResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Import Attendance")
      .WithDescription("Rows from the biometric device or a sheet, by employee number (at most 20,000). An existing day is overwritten. Good rows are saved; bad ones are listed in problems.");

    attendance.MapPost("/close-day", async (CloseAttendanceDayRequest request, ISender sender) =>
        (await sender.Send(new CloseAttendanceDayCommand(request.Date, request.DryRun))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("CloseAttendanceDay")
      .Produces<CloseAttendanceDayResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Close Attendance Day")
      .WithDescription("Gives everyone holding a post that day without a record one: on leave (approved leave), holiday, weekend (not a working day of their shift) or absent. "
        + "Safe to run again; dryRun=true only counts.");

    attendance.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeleteAttendanceCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Delete)
      .WithName("DeleteAttendance")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Attendance Day");
  }
}
