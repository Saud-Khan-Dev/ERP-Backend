public sealed record ProgressRequest(int ProgressPercentage, string? Notes);
public sealed record MyLinkDto(bool Linked, Guid? EmployeeId);
public sealed record MyRequestInput(Guid RequestTypeId, string? Subject, string? Description, System.Text.Json.JsonElement? RequestedData, Guid? SupportingDocumentId);

/// The employee portal (/me): any signed-in user whose account is linked to an employee record sees and acts on their
/// own record only. An account without an employee record gets 403.
public class SelfServiceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // the person's own record: never limited by an office scope
    var me = app.MapGroup("/me").WithTags("Self Service").RequireAuthorization().AllOffices();

    static async Task<Guid> Self(CurrentEmployee current, CancellationToken cancellationToken) => (await current.IdAsync(cancellationToken)).Value;

    // ---- is this account linked? (asked at every sign-in; never refused) ----

    me.MapGet("/link", async (CurrentEmployee current, CancellationToken ct) =>
    {
      var id = await current.TryIdAsync(ct);
      return Results.Ok(new MyLinkDto(id is not null, id?.Value));
    })
      .RequireSignedIn()
      .WithName("GetMyLink")
      .Produces<MyLinkDto>()
      .WithSummary("Is My Account Linked")
      .WithDescription("Whether the signed-in account is linked to an employee record (any signed-in user; an unlinked account gets linked=false, never a 403).");

    // ---- profile ----

    me.MapGet("/", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetMyOverviewQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyOverview")
      .Produces<MyOverviewDto>()
      .Produces(StatusCodes.Status403Forbidden)
      .WithSummary("My Overview")
      .WithDescription("Where I sit, what is waiting for me (tasks, leave, requests, reviews to acknowledge, evaluations to complete), this year's leave "
        + "balances, my latest pay slip, GP Fund balance and loans.");

    me.MapGet("/profile", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeQuery(await Self(current, ct), Own: true))).ToOk())
      .WithName("GetMyProfile")
      .Produces<GetEmployeeQueryResult>()
      .WithSummary("My Profile")
      .WithDescription("Changes are asked for through a profile correction request.");

    me.MapGet("/photo", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeePhotoQuery(await Self(current, ct)))).ToFile())
      .WithName("GetMyPhoto")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Photo");

    me.MapGet("/service-history", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetServiceHistoryQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyServiceHistory")
      .Produces<GetServiceHistoryQueryResult>()
      .WithSummary("My Service Record");

    me.MapGet("/documents", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeDocumentsQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyDocuments")
      .Produces<GetEmployeeDocumentsQueryResult>()
      .WithSummary("My Documents");

    me.MapPost("/documents", async ([Microsoft.AspNetCore.Mvc.FromForm] DocumentUploadForm form, CurrentEmployee current, ISender sender, CancellationToken ct) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new UploadEmployeeDocumentCommand(await Self(current, ct), form.Details(), file))).ToCreated(r => $"/me/documents/{r.Id}");
    })
      .DisableAntiforgery()
      .Accepts<DocumentUploadForm>("multipart/form-data")
      .WithName("UploadMyDocument")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload My Paper")
      .WithDescription("A paper the employee sends with a request (e.g. the proof for a profile correction). It joins the record unverified; HR verifies it.");

    me.MapGet("/documents/{id:guid}/content", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetDocumentContentQuery(id, await Self(current, ct)))).ToFile())
      .WithName("GetMyDocumentContent")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Download My Document");

    me.MapGet("/education", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEducationQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyEducation")
      .Produces<GetEducationQueryResult>()
      .WithSummary("My Education");

    // ---- attendance and leave ----

    me.MapGet("/shifts", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeShiftsQuery(await Self(current, ct)))).ToOk())
      .WithName("GetMyShifts")
      .Produces<GetEmployeeShiftsQueryResult>()
      .WithSummary("My Shifts");

    me.MapGet("/attendance", async (DateOnly from, DateOnly to, int? pageIndex, int? pageSize, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetAttendanceQuery(QueryParsing.Page(pageIndex, pageSize, 50), from, to, await Self(current, ct), null, null))).ToOk())
      .WithName("GetMyAttendance")
      .Produces<GetAttendanceQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Attendance");

    me.MapGet("/attendance/summary", async (int year, int month, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetAttendanceSummaryQuery(year, month, await Self(current, ct), null))).ToOk())
      .WithName("GetMyAttendanceSummary")
      .Produces<GetAttendanceSummaryQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Month's Attendance");

    me.MapGet("/leave/balances", async (int? year, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetLeaveBalancesQuery(await Self(current, ct), year))).ToOk())
      .WithName("GetMyLeaveBalances")
      .Produces<GetLeaveBalancesQueryResult>()
      .WithSummary("My Leave Balances");

    me.MapGet("/leave/applications", async (int? pageIndex, int? pageSize, string? status, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetLeaveApplicationsQuery(QueryParsing.Page(pageIndex, pageSize), await Self(current, ct),
          QueryParsing.ParseEnum<LeaveStatus>(status, "status"), null, null, null, null))).ToOk())
      .WithName("GetMyLeaveApplications")
      .Produces<GetLeaveApplicationsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Leave Applications");

    me.MapGet("/leave/days", async (DateOnly startDate, DateOnly endDate, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CountLeaveDaysQuery(await Self(current, ct), startDate, endDate))).ToOk())
      .WithName("CountMyLeaveDays")
      .Produces<LeaveDayCountResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Count My Leave Days")
      .WithDescription("The working days a range would take from my balance (holidays and days off left out).");

    me.MapPost("/leave/applications", async (LeaveApplicationInput leave, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ApplyLeaveCommand(await Self(current, ct), leave))).ToOk())
      .WithName("ApplyMyLeave")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply for Leave");

    me.MapPut("/leave/applications/{id:guid}", async (Guid id, LeaveApplicationInput leave, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ChangeLeaveApplicationCommand(id, leave, await Self(current, ct)))).ToOk())
      .WithName("ChangeMyLeave")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change My Leave Application")
      .WithDescription("Only while pending.");

    me.MapPost("/leave/applications/{id:guid}/cancel", async (Guid id, LeaveDecisionRequest? request, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new DecideLeaveCommand(id, LeaveDecision.Cancel, await Self(current, ct), request?.Reason))).ToOk())
      .WithName("CancelMyLeave")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Cancel My Leave")
      .WithDescription("A pending application, or an approved one that has not started (HR cancels leave already under way).");

    // ---- pay ----

    me.MapGet("/payslips", async (int? year, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetMyPayslipsQuery(await Self(current, ct), year))).ToOk())
      .WithName("GetMyPayslips")
      .Produces<GetMyPayslipsQueryResult>()
      .WithSummary("My Pay Slips")
      .WithDescription("Issued pay slips, newest first (reversed runs left out).");

    me.MapGet("/payslips/{transactionId:guid}", async (Guid transactionId, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetPayslipQuery(transactionId, await Self(current, ct), OnlyIssued: true))).ToOk())
      .WithName("GetMyPayslip")
      .Produces<PayslipDto>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Pay Slip");

    // printing one's own slip needs no export permission, but it is kept in the trail like any print
    me.MapPost("/payslips/{transactionId:guid}/printed", async (Guid transactionId, CurrentEmployee current, ISender sender, IAuditTrail audit, CancellationToken ct) =>
      {
        var self = await Self(current, ct);
        var slip = await sender.Send(new GetPayslipQuery(transactionId, self, OnlyIssued: true));
        if (!slip.IsSuccess)
          return Results.BadRequest(new { slip.Message });
        audit.Record(AuditActions.Printed, "Pay slip", transactionId, $"{slip.Value!.EmployeeNumber} {slip.Value.Period}", slip.Value.EmployeeName,
          self, slip.Value.EmployeeNumber, "1 row · own pay slip, through the portal");
        await audit.SaveAsync(ct);
        return Results.NoContent();
      })
      .WithName("NoteMyPayslipPrinted")
      .Produces(StatusCodes.Status204NoContent)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Note My Pay Slip Printed")
      .WithDescription("Keeps in the audit trail that the employee printed their own issued pay slip (no permission needed beyond the portal).");

    me.MapGet("/loans", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetLoansQuery(QueryParsing.Page(0, 100), await Self(current, ct), null, null))).ToOk())
      .WithName("GetMyLoans")
      .Produces<GetLoansQueryResult>()
      .WithSummary("My Loans");

    me.MapGet("/loans/{id:guid}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetLoanQuery(id, await Self(current, ct)))).ToOk())
      .WithName("GetMyLoan")
      .Produces<GetLoanQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Loan");

    me.MapGet("/gp-fund", async (CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetGpFundAccountQuery(null, await Self(current, ct)))).ToOk())
      .WithName("GetMyGpFund")
      .Produces<GetGpFundAccountQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My GP Fund");

    me.MapGet("/tax", async (Guid? taxYearId, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeTaxQuery(await Self(current, ct), taxYearId))).ToOk())
      .WithName("GetMyTax")
      .Produces<EmployeeTaxResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Income Tax So Far");

    // ---- tasks and requests ----

    me.MapGet("/tasks", async (int? pageIndex, int? pageSize, string? status, bool? openOnly, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetTasksQuery(QueryParsing.Page(pageIndex, pageSize), null, QueryParsing.ParseEnum<EmployeeTaskStatus>(status, "status"), null,
          false, openOnly ?? false, await Self(current, ct)))).ToOk())
      .WithName("GetMyTasks")
      .Produces<GetTasksQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Tasks");

    me.MapGet("/tasks/{id:guid}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetTaskQuery(id, await Self(current, ct)))).ToOk())
      .WithName("GetMyTask")
      .Produces<GetTaskQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Task");

    me.MapPost("/tasks/{id:guid}/progress", async (Guid id, ProgressRequest r, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new RecordTaskProgressCommand(id, r.ProgressPercentage, r.Notes, await Self(current, ct)))).ToOk())
      .WithName("RecordMyTaskProgress")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Report Progress on My Task")
      .WithDescription("100% completes the task.");

    me.MapGet("/request-types", async (ISender sender) => (await sender.Send(new GetRequestTypesQuery(false))).ToOk())
      .WithName("GetMyRequestTypes")
      .Produces<GetRequestTypesQueryResult>()
      .WithSummary("Request Types");

    me.MapGet("/requests", async (int? pageIndex, int? pageSize, string? status, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeRequestsQuery(QueryParsing.Page(pageIndex, pageSize), null, QueryParsing.ParseEnum<EmployeeRequestStatus>(status, "status"),
          null, await Self(current, ct)))).ToOk())
      .WithName("GetMyRequests")
      .Produces<GetEmployeeRequestsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("My Requests");

    me.MapGet("/requests/{id:guid}", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetEmployeeRequestQuery(id, await Self(current, ct)))).ToOk())
      .WithName("GetMyRequest")
      .Produces<GetEmployeeRequestQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("My Request");

    me.MapPost("/requests", async (MyRequestInput r, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SubmitEmployeeRequestCommand(await Self(current, ct), r.RequestTypeId, r.Subject, r.Description, r.RequestedData, r.SupportingDocumentId))).ToOk())
      .WithName("SubmitMyRequest")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Raise a Request")
      .WithDescription("requestedData: what is asked for, as a JSON object. supportingDocumentId: one of my documents (needed by some request types).");

    me.MapPost("/requests/{id:guid}/cancel", async (Guid id, CurrentEmployee current, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CancelEmployeeRequestCommand(id, await Self(current, ct)))).ToOk())
      .WithName("CancelMyRequest")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Withdraw My Request");
  }
}
