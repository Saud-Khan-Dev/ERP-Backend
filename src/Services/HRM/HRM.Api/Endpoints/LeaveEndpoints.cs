public sealed record SetLeaveEntitlementRequest(Guid LeaveTypeId, int Year, decimal EntitledDays);
public sealed record GrantLeaveEntitlementsRequest(int Year, Guid LeaveTypeId, decimal? EntitledDays, IReadOnlyList<Guid>? EmployeeIds);
public sealed record PostLeaveLedgerEntryRequest(Guid LeaveTypeId, int Year, LeaveTransactionType TransactionType, decimal Days, DateOnly? TransactionDate, string? Remarks);
public sealed record LeaveYearEndRequest(int Year, bool DryRun);

/// Leave types, yearly entitlements, the leave ledger (balances are entitlement + ledger), applications
/// (pending -> approved | rejected | cancelled) and the year-end carry-forward.
public class LeaveEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- leave types ----

    var types = app.MapGroup("/leave-types").WithTags("Leave Types");

    types.MapGet("/", async (ISender sender) => (await sender.Send(new GetLeaveTypesQuery())).ToOk())
      .RequireAuthorization()
      .WithName("GetLeaveTypes")
      .Produces<GetLeaveTypesQueryResult>()
      .WithSummary("Get Leave Types")
      .WithDescription("isBalanceTracked: drawn from a yearly entitlement (types with days per year or carry-forward).");

    types.MapPost("/", async (LeaveTypeInput type, ISender sender) =>
        (await sender.Send(new CreateLeaveTypeCommand(type))).ToCreated(r => $"/leave-types/{r.Id}"))
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithName("CreateLeaveType")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Create Leave Type")
      .WithDescription("affectsPayroll: approved days are deducted from pay (leave without pay).");

    types.MapPut("/{id:guid}", async (Guid id, LeaveTypeInput type, ISender sender) => (await sender.Send(new UpdateLeaveTypeCommand(id, type))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("UpdateLeaveType")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Update Leave Type")
      .WithDescription("Whether a type already used is paid or deducted from pay cannot change.");

    // ---- entitlements, balances, ledger ----

    app.MapGet("/employees/{id:guid}/leave-entitlements", async (Guid id, int? year, ISender sender) =>
        (await sender.Send(new GetLeaveEntitlementsQuery(id, year))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithTags("Leave Balances")
      .WithName("GetLeaveEntitlements")
      .Produces<GetLeaveEntitlementsQueryResult>()
      .WithSummary("Get Leave Entitlements");

    app.MapPut("/employees/{id:guid}/leave-entitlements", async (Guid id, SetLeaveEntitlementRequest request, ISender sender) =>
        (await sender.Send(new SetLeaveEntitlementCommand(id, request.LeaveTypeId, request.Year, request.EntitledDays))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithTags("Leave Balances")
      .WithName("SetLeaveEntitlement")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Leave Entitlement")
      .WithDescription("Creates the year's entitlement or changes its days (not below what the ledger has already drawn).");

    app.MapPost("/leave-entitlements/grant", async (GrantLeaveEntitlementsRequest request, ISender sender) =>
        (await sender.Send(new GrantLeaveEntitlementsCommand(request.Year, request.LeaveTypeId, request.EntitledDays, request.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithTags("Leave Balances")
      .WithName("GrantLeaveEntitlements")
      .Produces<GrantEntitlementsResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Grant Year's Entitlements")
      .WithDescription("Gives every employee in service (or the employeeIds given) the year's entitlement of the type; days default to the type's days per year. Existing entitlements are left alone.");

    app.MapGet("/employees/{id:guid}/leave-balances", async (Guid id, int? year, ISender sender) =>
        (await sender.Send(new GetLeaveBalancesQuery(id, year))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithTags("Leave Balances")
      .WithName("GetLeaveBalances")
      .Produces<GetLeaveBalancesQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Leave Balances")
      .WithDescription("The year's balances (this year by default). availableDays also subtracts leave applied for and not yet decided.");

    app.MapGet("/employees/{id:guid}/leave-ledger", async (Guid id, int? year, Guid? leaveTypeId, ISender sender) =>
        (await sender.Send(new GetLeaveLedgerQuery(id, year, leaveTypeId))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithTags("Leave Balances")
      .WithName("GetLeaveLedger")
      .Produces<GetLeaveLedgerQueryResult>()
      .WithSummary("Get Leave Ledger")
      .WithDescription("Every movement of the balances, newest first. Rows are never changed: corrections are adjustment rows.");

    app.MapPost("/employees/{id:guid}/leave-ledger", async (Guid id, PostLeaveLedgerEntryRequest r, ISender sender) =>
        (await sender.Send(new PostLeaveLedgerEntryCommand(id, r.LeaveTypeId, r.Year, r.TransactionType, r.Days, r.TransactionDate, r.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Approve)
      .WithTags("Leave Balances")
      .WithName("PostLeaveLedgerEntry")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Post Leave Ledger Entry")
      .WithDescription("transactionType = opening | accrual | encashment | adjustment. Opening and accrual add days, encashment takes them out, an adjustment is signed. "
        + "A balance cannot go below zero.");

    app.MapPost("/leave/year-end", async (LeaveYearEndRequest request, ISender sender) =>
        (await sender.Send(new RunLeaveYearEndCommand(request.Year, request.DryRun))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Approve)
      .WithTags("Leave Balances")
      .WithName("RunLeaveYearEnd")
      .Produces<LeaveYearEndResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Close Leave Year")
      .WithDescription("What is left of each balance of a past year is carried into the next year (types that allow it) or lapses. Balances already closed are skipped, so it can run again; dryRun=true only counts.");

    // ---- applications ----

    var applications = app.MapGroup("/leave-applications").WithTags("Leave Applications");

    applications.MapGet("/", async (int? pageIndex, int? pageSize, Guid? employeeId, string? status, Guid? leaveTypeId, DateOnly? from, DateOnly? to,
        Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetLeaveApplicationsQuery(QueryParsing.Page(pageIndex, pageSize), employeeId, QueryParsing.ParseEnum<LeaveStatus>(status, "status"),
          leaveTypeId, from, to, orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithName("GetLeaveApplications")
      .Produces<GetLeaveApplicationsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Leave Applications")
      .WithDescription("Newest first. status = pending | approved | rejected | cancelled; from / to: leave overlapping the range; orgUnitId takes in its sub-units.");

    applications.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetLeaveApplicationQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithName("GetLeaveApplication")
      .Produces<GetLeaveApplicationQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Leave Application");

    app.MapGet("/employees/{id:guid}/leave-days", async (Guid id, DateOnly startDate, DateOnly endDate, ISender sender) =>
        (await sender.Send(new CountLeaveDaysQuery(id, startDate, endDate))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.View)
      .WithTags("Leave Applications")
      .WithName("CountLeaveDays")
      .Produces<LeaveDayCountResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Count Leave Days")
      .WithDescription("The days a range would charge: the employee's working days that are not holidays, by calendar year.");

    app.MapPost("/employees/{id:guid}/leave-applications", async (Guid id, LeaveApplicationInput leave, ISender sender) =>
        (await sender.Send(new ApplyLeaveCommand(id, leave))).ToCreated(r => $"/leave-applications/{r.Id}"))
      .RequirePermission(PermissionCatalog.Attendance.Create)
      .WithTags("Leave Applications")
      .WithName("ApplyLeave")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply for Leave")
      .WithDescription("Days default to the working days in the range; a half day is days = 0.5 on one date. Leave drawn from a balance must fit what is available.");

    applications.MapPut("/{id:guid}", async (Guid id, LeaveApplicationInput leave, ISender sender) =>
        (await sender.Send(new ChangeLeaveApplicationCommand(id, leave))).ToOk())
      .RequirePermission(PermissionCatalog.Attendance.Edit)
      .WithName("ChangeLeaveApplication")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Leave Application")
      .WithDescription("Only while pending; the leave type cannot change.");

    foreach (var (decision, route, permission, summary, description) in new[]
    {
      (LeaveDecision.Approve, "approve", PermissionCatalog.Attendance.Approve, "Approve Leave",
        "Draws the days from the balance (checked again) and turns days already closed as absent into leave."),
      (LeaveDecision.Reject, "reject", PermissionCatalog.Attendance.Approve, "Reject Leave", "Only a pending application."),
      (LeaveDecision.Cancel, "cancel", PermissionCatalog.Attendance.Edit, "Cancel Leave",
        "A pending or approved application. Approved days go back to the balance and days closed as leave become absences again.")
    })
    {
      applications.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new DecideLeaveCommand(id, decision))).ToOk())
        .RequirePermission(permission)
        .WithName($"{decision}Leave")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary)
        .WithDescription(description);
    }
  }
}
