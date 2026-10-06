using Microsoft.AspNetCore.Mvc;

/// multipart/form-data body of a single-file upload.
public sealed class FileUploadForm
{
  public IFormFile? File { get; set; }
}

public sealed record ChangeEmploymentRequest(EmploymentType EmploymentType, EmploymentMethod? EmploymentMethod, Guid? ProjectId);
public sealed record CorrectEmployeeNumberRequest(string EmployeeNumber);
public sealed record LinkUserRequest(Guid UserId);
public sealed record RecordPayChangeRequest(Guid PayScaleStageId, decimal? BasicPay, DateOnly EffectiveFrom, string Reason, string? OrderNumber, DateOnly? NextIncrementDate);
public sealed record SetNextIncrementRequest(DateOnly? NextIncrementDate);
public sealed record AnnualIncrementRequest(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? EmployeeIds);
public sealed record PayRevisionRequest(DateOnly EffectiveDate, bool DryRun, IReadOnlyList<Guid>? GradeIds);

/// The employee master: registration (with the first appointment), personal details, contacts, addresses, family,
/// the Identity login link, the photo and the pay record.
public class EmployeeEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    var employees = app.MapGroup("/employees").WithTags("Employees");

    employees.MapGet("/", async (int? pageIndex, int? pageSize, string? search, string? employmentType, [FromQuery(Name = "employmentStatus")] string[]? employmentStatus,
        bool? includeInactive, Guid? orgUnitId, bool? includeSubUnits, Guid? designationId, Guid? gradeId, Guid? postId, string? gender, bool? unplaced,
        int? retiringWithinMonths, string? sortBy, string? sortDir, ISender sender) =>
        (await sender.Send(new GetEmployeesQuery(
          QueryParsing.Page(pageIndex, pageSize), search, QueryParsing.ParseEnum<EmploymentType>(employmentType, "employmentType"),
          (employmentStatus ?? []).SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(s => QueryParsing.ParseEnum<EmploymentStatus>(s, "employmentStatus")!.Value).Distinct().ToList(),
          includeInactive ?? false, orgUnitId, includeSubUnits ?? true, designationId, gradeId, postId,
          QueryParsing.ParseEnum<Gender>(gender, "gender"), unplaced, retiringWithinMonths,
          QueryParsing.ParseEnum<EmployeeSort>(sortBy, "sortBy") ?? EmployeeSort.Number, QueryParsing.ParseDescending(sortDir) ?? false))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.Attendance.View, PermissionCatalog.Payroll.View)
      .WithName("GetEmployees")
      .Produces<GetEmployeesQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Employees")
      .WithDescription("The employee register (pageSize 1-200). search = part of the number or name, or the CNIC. employmentStatus repeats (active,on_leave ...). "
        + "orgUnitId / designationId / gradeId / postId filter on today's post (the unit includes sub-units unless includeSubUnits=false); unplaced=true lists those without a regular post. "
        + "retiringWithinMonths = superannuation within that many months. sortBy = number | name | registered | retirement.");

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
        (await sender.Send(new ChangeEmploymentCommand(id, request.EmploymentType, request.EmploymentMethod, request.ProjectId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ChangeEmployment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Employment Type")
      .WithDescription("Corrects the employment type / method / project. A regularization is recorded as an HR action so it enters the service history.");

    employees.MapPut("/{id:guid}/number", async (Guid id, CorrectEmployeeNumberRequest request, ISender sender) =>
        (await sender.Send(new CorrectEmployeeNumberCommand(id, request.EmployeeNumber))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("CorrectEmployeeNumber")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Correct Employee Number");

    employees.MapPost("/{id:guid}/user", async (Guid id, LinkUserRequest request, ISender sender) =>
        (await sender.Send(new LinkEmployeeUserCommand(id, request.UserId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("LinkEmployeeUser")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Link Login")
      .WithDescription("Links the employee to an Identity user account (no cross-service check: give the id shown in Administration › Users). Self-service (/me) finds the employee this way or by the token's employee claim.");

    employees.MapDelete("/{id:guid}/user", async (Guid id, ISender sender) =>
        (await sender.Send(new LinkEmployeeUserCommand(id, null))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UnlinkEmployeeUser")
      .Produces<UpdatedResult>()
      .WithSummary("Unlink Login");

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
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithTags("Pay Records")
      .WithName("RecordPayChange")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Pay Change")
      .WithDescription("A new pay position from a date (correction, advance increments, personal pay). Promotions and appointments fix the pay themselves.");

    var pay = app.MapGroup("/pay-records").WithTags("Pay Records");

    pay.MapPut("/{id:guid}/next-increment", async (Guid id, SetNextIncrementRequest request, ISender sender) =>
        (await sender.Send(new SetNextIncrementDateCommand(id, request.NextIncrementDate))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("SetNextIncrementDate")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Set Next Increment Date")
      .WithDescription("e.g. an increment withheld as a penalty, or deferred.");

    pay.MapPost("/annual-increment", async (AnnualIncrementRequest request, ISender sender) =>
        (await sender.Send(new RunAnnualIncrementCommand(request.EffectiveDate, request.DryRun, request.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Approve)
      .WithName("RunAnnualIncrement")
      .Produces<PayRunResult>()
      .WithSummary("Run Annual Increment")
      .WithDescription("Moves every employee in service whose next increment is due by effectiveDate one stage up (those at the top of the scale, suspended or out of service are listed as skipped). dryRun=true shows the result without saving.");

    pay.MapPost("/revision", async (PayRevisionRequest request, ISender sender) =>
        (await sender.Send(new ApplyPayRevisionCommand(request.EffectiveDate, request.DryRun, request.GradeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Approve)
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
    employees.MapPost($"/{{id:guid}}/{route}", async (Guid id, TInput input, ISender sender) => (await add(id, input, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName($"Add{name}")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Add {name}");

    employees.MapPut($"/{{id:guid}}/{route}/{{childId:guid}}", async (Guid id, Guid childId, TInput input, ISender sender) => (await update(id, childId, input, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName($"Update{name}")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary($"Update {name}");

    employees.MapDelete($"/{{id:guid}}/{route}/{{childId:guid}}", async (Guid id, Guid childId, ISender sender) => (await remove(id, childId, sender)).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
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
