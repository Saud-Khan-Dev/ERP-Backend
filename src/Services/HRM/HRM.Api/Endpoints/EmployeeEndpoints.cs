using Microsoft.AspNetCore.Mvc;

/// multipart/form-data body of a single-file upload.
public sealed class FileUploadForm
{
  public IFormFile? File { get; set; }
}

public sealed record ChangeEmploymentRequest(Guid EmploymentTypeId, Guid? EmploymentMethodId, Guid? ProjectId, string? Reason);
public sealed record CorrectEmployeeNumberRequest(string EmployeeNumber, string? Reason);
public sealed record SetTermRequest(DateOnly? TermFrom, DateOnly? TermTo, string? LendingDepartment, string? Reason);
public sealed record LinkUserRequest(Guid UserId);
public sealed record RecordPayChangeRequest(Guid PayScaleStageId, decimal? BasicPay, DateOnly EffectiveFrom, string Reason, string? OrderNumber, DateOnly? NextIncrementDate);
public sealed record SetNextIncrementRequest(DateOnly? NextIncrementDate, string? Reason);
public sealed record AnnualIncrementRequest(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? EmployeeIds);
public sealed record PayRevisionRequest(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? GradeIds);

/// The employee master: registration (with the first appointment), personal details, contacts, addresses, family,
/// the Identity login link, the photo and the pay record.
public class EmployeeEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var employees = app.MapGroup("/employees").WithTags("Employees");

    employees.MapGet("/", async (int? pageIndex, int? pageSize, string? search, Guid? employmentTypeId, [FromQuery(Name = "employmentStatus")] string[]? employmentStatus,
        bool? includeInactive, Guid? orgUnitId, bool? includeSubUnits, Guid? designationId, Guid? gradeId, Guid? postId, string? gender, bool? unplaced,
        int? retiringWithinMonths, string? sortBy, string? sortDir, bool? inService, int? termEndingWithinDays, bool? withoutDateOfBirth, bool? pastRetirement,
        bool? incrementOverdue, ISender sender) =>
        (await sender.Send(new GetEmployeesQuery(
          QueryParsing.Page(pageIndex, pageSize), search, employmentTypeId,
          (employmentStatus ?? []).SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(s => QueryParsing.ParseEnum<EmploymentStatus>(s, "employmentStatus")!.Value).Distinct().ToList(),
          includeInactive ?? false, orgUnitId, includeSubUnits ?? true, designationId, gradeId, postId,
          QueryParsing.ParseEnum<Gender>(gender, "gender"), unplaced, retiringWithinMonths,
          QueryParsing.ParseEnum<EmployeeSort>(sortBy, "sortBy") ?? EmployeeSort.Number, QueryParsing.ParseDescending(sortDir) ?? false,
          inService, termEndingWithinDays, withoutDateOfBirth, pastRetirement, incrementOverdue))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View)
      .WithName("GetEmployees")
      .Produces<GetEmployeesQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Employees")
      .WithDescription("The employee register (pageSize 1-200). search = part of the number or name, or the CNIC. employmentStatus repeats (active,on_leave ...). "
        + "orgUnitId / designationId / gradeId / postId filter on today's post (the unit includes sub-units unless includeSubUnits=false); unplaced=true lists those without a regular post. "
        + "retiringWithinMonths = in service and superannuation within that many months (already past it included); pastRetirement / withoutDateOfBirth = in service and "
        + "past the retirement age / without a date of birth; inService = true | false; termEndingWithinDays = the contract or deputation term ends within that many days; "
        + "incrementOverdue = in service and the next increment date has passed. "
        + "sortBy = number | name | registered | retirement.");

    employees.MapGet("/next-number", async (ISender sender) => (await sender.Send(new GetNextEmployeeNumberQuery())).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithName("GetNextEmployeeNumber")
      .Produces<GetNextEmployeeNumberQueryResult>()
      .WithSummary("Preview Next Employee Number")
      .WithDescription("The number a registration would get when none is typed (EMP-### like the Identity service). A preview only: nothing is reserved.");

    employees.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeeQuery(id))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View)
      .WithName("GetEmployee")
      .Produces<GetEmployeeQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Employee")
      .WithDescription("The full record: personal details, contacts, addresses, family, today's post, current pay, service start and superannuation date.");

    employees.MapPost("/", async (RegisterEmployeeCommand command, ISender sender) =>
        (await sender.Send(command)).ToCreated(r => $"/employees/{r.Id}"))
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithName("RegisterEmployee")
      .Produces<RegisterEmployeeCommandResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Register Employee")
      .WithDescription("Registers an employee in one transaction: personal details, contacts, addresses, emergency contacts, family and, when given, the appointment "
        + "(service-history row, regular assignment to the post, the starting pay on the post's scale - stage 0 unless stageNumber or basicPay is given). "
        + "Leave employeeNumber empty to have one issued. CNIC is stored as 35202-1234567-1.");

    employees.MapPut("/{id:guid}", async (Guid id, PersonalDetailsInput personal, ISender sender) =>
        (await sender.Send(new UpdatePersonalDetailsCommand(id, personal))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UpdatePersonalDetails")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Personal Details");

    employees.MapPut("/{id:guid}/employment", async (Guid id, ChangeEmploymentRequest request, ISender sender) =>
        (await sender.Send(new ChangeEmploymentCommand(id, request.EmploymentTypeId, request.EmploymentMethodId, request.ProjectId, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ChangeEmployment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Employment Type")
      .WithDescription("Corrects a mistake in the employment type / method / project, with the reason (kept in the change log). Making a contract or other "
        + "employee regular is refused here: it is the Regularization HR action, approved and recorded in the service book.");

    employees.MapPut("/{id:guid}/term", async (Guid id, SetTermRequest request, ISender sender) =>
        (await sender.Send(new SetEmployeeTermCommand(id, request.TermFrom, request.TermTo, request.LendingDepartment, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("SetEmployeeTerm")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Contract or Deputation Term")
      .WithDescription("The engagement period of a contract, deputation-in, ad hoc, daily-wage or project employee (an extension is a new end date), and a "
        + "deputationist's parent department. Kept in the change log.");

    employees.MapGet("/{id:guid}/history", async (Guid id, HttpRequest request, ISender sender) =>
        (await sender.Send(new GetEmployeeHistoryQuery(id, AuditQueries.FilterFrom(request)))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View)
      .WithName("GetEmployeeHistory")
      .Produces<AuditResponse>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Employee History")
      .WithDescription("Everything recorded about the employee in the audit trail, newest first: the record itself and everything that hangs off it "
        + "(contacts, family, bank accounts, pay, documents, posts, HR actions, leave, attendance, loans, GP Fund, pay slips, reviews, tasks, requests). "
        + "Filters as GET /audit: before, from, to, actorId, action, kind, search, pageSize. Without HR_SENSITIVE.VIEW the values of personal and pay "
        + "facts read \"Restricted\".");

    employees.MapPut("/{id:guid}/number", async (Guid id, CorrectEmployeeNumberRequest request, ISender sender) =>
        (await sender.Send(new CorrectEmployeeNumberCommand(id, request.EmployeeNumber, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("CorrectEmployeeNumber")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Employee Number");

    // linking a sign-in account decides whose portal (/me) the account opens, so it is an account act (IAM_USERS.EDIT);
    // the screen then tells Identity too (PUT /users/{userId}/employee-link), which shows the HR number as the account's code
    employees.MapPost("/{id:guid}/user", async (Guid id, LinkUserRequest request, ISender sender) =>
        (await sender.Send(new LinkEmployeeUserCommand(id, request.UserId))).ToOk())
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("LinkEmployeeUser")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Link Sign-in Account")
      .WithDescription("Links the employee to an Identity user account (IAM_USERS.EDIT). Your own account is refused, and so is an account linked to "
        + "another employee; relinking the same employee to another account replaces the link. Self-service (/me) follows the token's employee claim.");

    employees.MapDelete("/{id:guid}/user", async (Guid id, ISender sender) =>
        (await sender.Send(new LinkEmployeeUserCommand(id, null))).ToOk())
      .RequirePermission(PermissionCatalog.Users.Edit)
      .WithName("UnlinkEmployeeUser")
      .Produces<UpdatedResult>()
      .WithSummary("Unlink Sign-in Account");

    employees.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetEmployeeProfileStatusCommand(id, RecordStatus.Active))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ActivateEmployee")
      .Produces<UpdatedResult>()
      .WithSummary("Activate Profile");

    employees.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        (await sender.Send(new SetEmployeeProfileStatusCommand(id, RecordStatus.Inactive))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Delete)
      .WithName("DeactivateEmployee")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Deactivate Profile")
      .WithDescription("Nothing is deleted: the profile and its history stay on record but are no longer offered. Someone still holding a post must be separated first.");

    employees.MapPost("/{id:guid}/photo", async (Guid id, [FromForm] FileUploadForm form, ISender sender) =>
    {
      await using var content = Uploads.Open(form.File, out var file);
      return (await sender.Send(new UploadEmployeePhotoCommand(id, file))).ToOk();
    })
      .DisableAntiforgery()
      .Accepts<FileUploadForm>("multipart/form-data")
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UploadEmployeePhoto")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Upload Photo")
      .WithDescription("jpg, png or webp, up to 5 MB. Replaces the current photo.");

    employees.MapGet("/{id:guid}/photo", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeePhotoQuery(id))).ToFile())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View)
      .WithName("GetEmployeePhoto")
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Photo");

    // ---- contacts, addresses, emergency contacts, family ----

    MapChild(employees, "contacts", "Contact",
      (Guid id, ContactInput input, ISender sender) => sender.Send(new AddContactCommand(id, input)),
      (Guid id, Guid childId, ContactInput input, ISender sender) => sender.Send(new UpdateContactCommand(id, childId, input)),
      (Guid id, Guid childId, ISender sender) => sender.Send(new RemoveContactCommand(id, childId)));
    MapChild(employees, "addresses", "Address",
      (Guid id, AddressInput input, ISender sender) => sender.Send(new AddAddressCommand(id, input)),
      (Guid id, Guid childId, AddressInput input, ISender sender) => sender.Send(new UpdateAddressCommand(id, childId, input)),
      (Guid id, Guid childId, ISender sender) => sender.Send(new RemoveAddressCommand(id, childId)));
    MapChild(employees, "emergency-contacts", "EmergencyContact",
      (Guid id, EmergencyContactInput input, ISender sender) => sender.Send(new AddEmergencyContactCommand(id, input)),
      (Guid id, Guid childId, EmergencyContactInput input, ISender sender) => sender.Send(new UpdateEmergencyContactCommand(id, childId, input)),
      (Guid id, Guid childId, ISender sender) => sender.Send(new RemoveEmergencyContactCommand(id, childId)));
    MapChild(employees, "family-members", "FamilyMember",
      (Guid id, FamilyMemberInput input, ISender sender) => sender.Send(new AddFamilyMemberCommand(id, input)),
      (Guid id, Guid childId, FamilyMemberInput input, ISender sender) => sender.Send(new UpdateFamilyMemberCommand(id, childId, input)),
      (Guid id, Guid childId, ISender sender) => sender.Send(new RemoveFamilyMemberCommand(id, childId)));

    // ---- pay record ----

    employees.MapGet("/{id:guid}/pay-records", async (Guid id, ISender sender) => (await sender.Send(new GetPayRecordsQuery(id))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Payroll.View)
      .WithTags("Pay Records")
      .WithName("GetPayRecords")
      .Produces<GetPayRecordsQueryResult>()
      .WithSummary("Get Pay Record")
      .WithDescription("Every pay position of the employee, newest first: stage, basic pay, why it changed.");

    employees.MapPost("/{id:guid}/pay-records", async (Guid id, RecordPayChangeRequest request, ISender sender) =>
        (await sender.Send(new RecordPayChangeCommand(id, request.PayScaleStageId, request.BasicPay, request.EffectiveFrom, request.Reason,
          request.OrderNumber, request.NextIncrementDate))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Pay)
      .WithTags("Pay Records")
      .WithName("RecordPayChange")
      .Produces<RecordPayChangeResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Pay Change")
      .WithDescription("A new pay position from a date (correction, advance increments) - a money change, so HR.PAY; never on your own record. The stage must belong to the scale of the "
        + "grade of the post held that day. Kept in the change log; payrollImpact lists posted months a back-dated change reaches. Promotions and appointments fix the pay themselves.");

    var pay = app.MapGroup("/pay-records").WithTags("Pay Records");

    pay.MapPut("/{id:guid}/next-increment", async (Guid id, SetNextIncrementRequest request, ISender sender) =>
        (await sender.Send(new SetNextIncrementDateCommand(id, request.NextIncrementDate, request.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Pay)
      .WithName("SetNextIncrementDate")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Next Increment Date")
      .WithDescription("e.g. an increment withheld as a penalty, or deferred (HR.PAY; kept in the audit trail; never on your own record). The date set is the one before any leave-without-pay deferral.");

    pay.MapPost("/annual-increment", async (AnnualIncrementRequest request, ISender sender) =>
        (await sender.Send(new RunAnnualIncrementCommand(request.EffectiveDate, request.DryRun, request.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Run)
      .WithName("RunAnnualIncrement")
      .Produces<PayRunResult>()
      .WithSummary("Run Annual Increment")
      .WithDescription("Moves every employee in service whose next increment is due by effectiveDate one stage up (those at the top of the scale, suspended or out of service are listed as skipped). dryRun=true shows the result without saving.");

    pay.MapPost("/revision", async (PayRevisionRequest request, ISender sender) =>
        (await sender.Send(new ApplyPayRevisionCommand(request.EffectiveDate, request.DryRun, request.GradeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Run)
      .WithName("ApplyPayRevision")
      .Produces<PayRunResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply Pay Revision")
      .WithDescription("Everyone on an older scale of a grade whose revised scale starts on effectiveDate moves to the same stage of the revised scale. Notify the revised scales first. dryRun=true shows the result without saving.");
  }

  private static void MapChild<TInput>(
      RouteGroupBuilder employees,
      string route,
      string name,
      Func<Guid, TInput, ISender, Task<Result<CreatedResult>>> add,
      Func<Guid, Guid, TInput, ISender, Task<Result<UpdatedResult>>> update,
      Func<Guid, Guid, ISender, Task<Result<UpdatedResult>>> remove)
  {
    // contacts, addresses and family are personal facts: changing them needs seeing them (HR_SENSITIVE.VIEW) as well
    employees.MapPost($"/{{id:guid}}/{route}", async (Guid id, TInput input, ISender sender) => (await add(id, input, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .RequirePermission(PermissionCatalog.HrSensitive.View)
      .WithName($"Add{name}")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Add {name}");

    employees.MapPut($"/{{id:guid}}/{route}/{{childId:guid}}", async (Guid id, Guid childId, TInput input, ISender sender) => (await update(id, childId, input, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .RequirePermission(PermissionCatalog.HrSensitive.View)
      .WithName($"Update{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Update {name}");

    employees.MapDelete($"/{{id:guid}}/{route}/{{childId:guid}}", async (Guid id, Guid childId, ISender sender) => (await remove(id, childId, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .RequirePermission(PermissionCatalog.HrSensitive.View)
      .WithName($"Remove{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Remove {name}");
  }
}

public static class Uploads
{
  public static Stream Open(IFormFile? file, out UploadedFile upload)
  {
    if (file is null || file.Length == 0)
      throw new BadHttpRequestException("Attach the file in the 'file' form field.");

    var stream = file.OpenReadStream();
    upload = new UploadedFile(stream, file.FileName, file.ContentType, file.Length);
    return stream;
  }

  public static IResult ToFile(this Result<StoredFile> result) =>
      result.IsSuccess
        ? Results.File(result.Value!.Content, result.Value.ContentType, result.Value.FileName)
        : Results.BadRequest(new { result.Message });
}
