public sealed record RecordServiceEventRequest(
  Guid EventTypeId,
  DateOnly EffectiveDate,
  Guid? OldPostId,
  Guid? NewPostId,
  Guid? OldGradeId,
  Guid? NewGradeId,
  Guid? OldOrgUnitId,
  Guid? NewOrgUnitId,
  Guid? RecruitmentMethodId,
  string? ExternalReferenceOrg,
  string? OrderNumber,
  Guid? SupportingDocumentId,
  string? Reason,
  string? Remarks);

public sealed record CreateAssignmentRequest(
  Guid PostId,
  AssignmentType AssignmentType,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  string? OrderNumber,
  Guid? OrderDocumentId,
  string? Remarks);

public sealed record EndAssignmentRequest(DateOnly LastDay);
public sealed record CancelRequest(string? Remarks);
public sealed record ApproveHrActionRequest(Guid? ApprovalRequestId);

public sealed record RecordSeparationRequest(
  SeparationType SeparationType,
  DateOnly SeparationDate,
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  EmploymentStatus? EmploymentStatus,
  Guid? EventTypeId,
  Guid? SupportingDocumentId);

public sealed record UpdateSeparationRequest(
  string? OrderNumber,
  string? Reason,
  int? NoticePeriodDays,
  decimal? FinalSettlementAmount,
  decimal? OutstandingLoanAmount,
  decimal? LeaveEncashmentAmount,
  string? PensionReference);

/// The service record: the append-only service history, post assignments, HR actions (draft -> pending -> approved ->
/// applied) and separations.
public class ServiceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- service history ----

    app.MapGet("/employees/{id:guid}/service-history", async (Guid id, ISender sender) => (await sender.Send(new GetServiceHistoryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Service History")
      .WithName("GetServiceHistory")
      .Produces<GetServiceHistoryQueryResult>()
      .WithSummary("Get Service History")
      .WithDescription("Every service event of the employee, newest first. Rows are never changed: corrections are new rows.");

    app.MapPost("/employees/{id:guid}/service-history", async (Guid id, RecordServiceEventRequest r, ISender sender) =>
        (await sender.Send(new RecordServiceEventCommand(id, r.EventTypeId, r.EffectiveDate, r.OldPostId, r.NewPostId, r.OldGradeId, r.NewGradeId,
          r.OldOrgUnitId, r.NewOrgUnitId, r.RecruitmentMethodId, r.ExternalReferenceOrg, r.OrderNumber, r.SupportingDocumentId, r.Reason, r.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("Service History")
      .WithName("RecordServiceEvent")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Past Service Event")
      .WithDescription("Back-fills the service book (or records a correcting row). History only: the current post, pay and status change through HR actions.");

    // ---- assignments ----

    app.MapGet("/employees/{id:guid}/assignments", async (Guid id, bool? includeCancelled, ISender sender) =>
        (await sender.Send(new GetAssignmentsQuery(id, includeCancelled ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Assignments")
      .WithName("GetAssignments")
      .Produces<GetAssignmentsQueryResult>()
      .WithSummary("Get Posts Held");

    app.MapPost("/employees/{id:guid}/assignments", async (Guid id, CreateAssignmentRequest r, ISender sender) =>
        (await sender.Send(new CreateAssignmentCommand(id, r.PostId, r.AssignmentType, r.EffectiveFrom, r.EffectiveTo, r.OrderNumber, r.OrderDocumentId, r.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithTags("Assignments")
      .WithName("CreateAssignment")
      .Produces<CreatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Give Charge of Post")
      .WithDescription("Acting, additional charge or look-after arrangement beside the regular post. A regular assignment here only records an existing holder; appointments and moves go through HR actions.");

    var assignments = app.MapGroup("/assignments").WithTags("Assignments");

    assignments.MapPost("/{id:guid}/end", async (Guid id, EndAssignmentRequest request, ISender sender) =>
        (await sender.Send(new EndAssignmentCommand(id, request.LastDay))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("EndAssignment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Relieve of Post")
      .WithDescription("The last day on the post.");

    assignments.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest request, ISender sender) =>
        (await sender.Send(new CancelAssignmentCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Delete)
      .WithName("CancelAssignment")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Assignment")
      .WithDescription("An assignment recorded by mistake stops counting but stays on record. Not once paid on.");

    // ---- HR actions ----

    var actions = app.MapGroup("/hr-actions").WithTags("HR Actions");

    actions.MapGet("/", async (int? pageIndex, int? pageSize, string? status, string? actionType, Guid? employeeId, DateOnly? from, DateOnly? to,
        string? search, Guid? orgUnitId, string? view, string? sortDir, ISender sender) =>
        (await sender.Send(new GetHrActionsQuery(QueryParsing.Page(pageIndex, pageSize), QueryParsing.ParseEnum<HrActionStatus>(status, "status"),
          QueryParsing.ParseEnum<HrActionType>(actionType, "actionType"), employeeId, from, to, search, orgUnitId,
          QueryParsing.ParseEnum<HrActionView>(view, "view"), QueryParsing.ParseDescending(sortDir) == false))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetHrActions")
      .Produces<GetHrActionsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get HR Actions")
      .WithDescription("Newest first (sortDir=asc: oldest first). status = draft | pending | approved | rejected | cancelled | applied; from / to on the effective date; "
        + "search = order number, employee number or name; orgUnitId = the old or new post's unit with its sub-units; view = mine | to_approve | to_apply | waiting "
        + "(approved, taking effect on a later date).");

    actions.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetHrActionQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetHrAction")
      .Produces<GetHrActionQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get HR Action");

    app.MapGet("/employees/{id:guid}/hr-action-options", async (Guid id, DateOnly? date, ISender sender) =>
        (await sender.Send(new GetHrActionOptionsQuery(id, date))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("HR Actions")
      .WithName("GetHrActionOptions")
      .Produces<HrActionOptionsDto>()
      .WithSummary("HR Actions That Fit")
      .WithDescription("For the draft dialog: every kind of action with whether it fits the person on the date (and why not), the post held, the pay drawn, "
        + "the post kept on lien, and the service events added in Configuration.");

    app.MapGet("/employees/{id:guid}/hr-action-posts", async (Guid id, string actionType, DateOnly date, string? search, ISender sender) =>
        (await sender.Send(new GetHrActionPostsQuery(id, QueryParsing.ParseEnum<HrActionType>(actionType, "actionType")
          ?? throw new BadHttpRequestException("actionType is required."), date, search))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("HR Actions")
      .WithName("GetHrActionPosts")
      .Produces<IReadOnlyList<HrActionPostDto>>()
      .WithSummary("Posts an Action Can Move To")
      .WithDescription("The posts of the grade the action needs (promotion: higher, demotion: lower, transfer: the same), with seats and the reason a post cannot be "
        + "taken (full, frozen, no pay scale in force). search = code, designation or unit.");

    app.MapPost("/employees/{id:guid}/hr-actions/preview", async (Guid id, HrActionInput action, ISender sender) =>
        (await sender.Send(new PreviewHrActionQuery(id, action))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("HR Actions")
      .WithName("PreviewHrAction")
      .Produces<HrActionPreviewDto>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Preview HR Action")
      .WithDescription("What the action would do, without saving: the grade and pay before and after, the date, and the reason it cannot be done (the same rules as the draft and the apply).");

    app.MapPost("/employees/{id:guid}/hr-actions", async (Guid id, HrActionInput action, ISender sender) =>
        (await sender.Send(new CreateHrActionCommand(id, action))).ToCreated(r => $"/hr-actions/{r.Id}"))
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("HR Actions")
      .WithName("CreateHrAction")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Draft HR Action")
      .WithDescription("appointment, joining, transfer, promotion, demotion, deputation_in / _out, regularization, lwop, suspension, reinstatement, retirement, resignation, "
        + "termination, death, dismissal, removal_from_service, compulsory_retirement, premature_retirement, end_of_contract, deputation_end, other (a service event added in "
        + "Configuration: eventTypeId). The same rules as the apply are checked now (grade, free seat, move date, the person's state, age at appointment). "
        + "lwop needs endDate; deputation needs externalReferenceOrg; regularization employmentMethod.");

    actions.MapPut("/{id:guid}", async (Guid id, HrActionInput action, ISender sender) => (await sender.Send(new UpdateHrActionCommand(id, action))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("UpdateHrAction")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Draft HR Action");

    foreach (var (step, route, permission, summary) in new[]
    {
      (HrActionStep.Submit, "submit", PermissionCatalog.Hr.Edit, "Submit HR Action"),
      (HrActionStep.Return, "return", PermissionCatalog.Hr.Approve, "Return HR Action to Draft"),
      (HrActionStep.Reject, "reject", PermissionCatalog.Hr.Approve, "Reject HR Action"),
      (HrActionStep.Cancel, "cancel", PermissionCatalog.Hr.Edit, "Cancel HR Action")
    })
    {
      actions.MapPost($"/{{id:guid}}/{route}", async (Guid id, CancelRequest? request, ISender sender) =>
          (await sender.Send(new MoveHrActionCommand(id, step, null, request?.Remarks))).ToOk())
        .RequirePermission(permission)
        .WithName($"{step}HrAction")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary(summary)
        .WithDescription(step == HrActionStep.Reject ? "remarks (the reason) is required." : "remarks: optional, kept with who and when.");
    }

    actions.MapPost("/{id:guid}/approve", async (Guid id, ApproveHrActionRequest? request, ISender sender) =>
        (await sender.Send(new MoveHrActionCommand(id, HrActionStep.Approve, request?.ApprovalRequestId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Approve)
      .WithName("ApproveHrAction")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve HR Action")
      .WithDescription("The approver is the signed-in officer, and may not be the one who drafted or submitted it (Hrm:RequireSeparateApprover, which follows "
        + "Payroll:RequireSeparateApprover unless set). The rules are checked again. approvalRequestId optionally points at a platform approval workflow item.");

    actions.MapPost("/{id:guid}/apply", async (Guid id, HrActionApplyInput? input, ISender sender) =>
        (await sender.Send(new ApplyHrActionCommand(id, input ?? HrActionApplyInput.None))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ApplyHrAction")
      .Produces<ApplyHrActionResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Apply HR Action")
      .WithDescription("Carries out an approved action in one transaction: the service-history row (its id is returned), the post change with the pay fixed in the new grade, "
        + "leave without pay as an approved leave of the unpaid type (payroll stops the pay for those days), the service status, or the separation (its checklist is "
        + "returned). An action that changes the status or the employment type is applied on or after its date. payrollImpact lists posted payroll months the "
        + "change reaches (arrears or a recovery to work out).");

    // ---- separations ----

    app.MapGet("/separations", async (int? pageIndex, int? pageSize, string? type, DateOnly? from, DateOnly? to, string? search, bool? settled, ISender sender) =>
        (await sender.Send(new GetSeparationsQuery(QueryParsing.Page(pageIndex, pageSize), QueryParsing.ParseEnum<SeparationType>(type, "type"), from, to, search, settled))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Separations")
      .WithName("GetSeparations")
      .Produces<GetSeparationsQueryResult>()
      .WithSummary("Get Separations")
      .WithDescription("search = employee number or name; settled=false lists those whose final settlement is not worked out yet.");

    app.MapGet("/employees/{id:guid}/separation", async (Guid id, ISender sender) => (await sender.Send(new GetEmployeeSeparationQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Separations")
      .WithName("GetEmployeeSeparation")
      .Produces<GetSeparationQueryResult>()
      .WithSummary("Get Employee Separation");

    app.MapGet("/employees/{id:guid}/separation/checklist", async (Guid id, ISender sender) => (await sender.Send(new GetSeparationChecklistQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithTags("Separations")
      .WithName("GetSeparationChecklist")
      .Produces<SeparationChecklistDto>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Separation Checklist")
      .WithDescription("What still needs a person: the sign-in account, assets in custody, loans, the GP Fund final payment, posts headed, signing posts, the final "
        + "settlement, leave running past the last day, appraisals; and the posted payroll after the last day (to recover).");

    app.MapPost("/employees/{id:guid}/separation", async (Guid id, RecordSeparationRequest r, ISender sender) =>
        (await sender.Send(new RecordSeparationCommand(id, r.SeparationType, r.SeparationDate, r.OrderNumber, r.Reason, r.NoticePeriodDays,
          r.EmploymentStatus, r.EventTypeId, r.SupportingDocumentId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Approve)
      .WithTags("Separations")
      .WithName("RecordSeparation")
      .Produces<RecordSeparationResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Separation")
      .WithDescription("Ends the employee's service on the separation date (the last day): posts relieved, pay closed, shift ended, status set (retired, resigned, "
        + "terminated, dismissed, removed, contract ended, repatriated, deceased), service-history row written. Open HR actions, pending and later leave, open tasks, "
        + "pending requests and empty draft appraisals are closed; the checklist of what still needs a person is returned.");

    app.MapPut("/separations/{id:guid}", async (Guid id, UpdateSeparationRequest r, ISender sender) =>
        (await sender.Send(new UpdateSeparationCommand(id, r.OrderNumber, r.Reason, r.NoticePeriodDays, r.FinalSettlementAmount,
          r.OutstandingLoanAmount, r.LeaveEncashmentAmount, r.PensionReference))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithTags("Separations")
      .WithName("UpdateSeparation")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Settlement")
      .WithDescription("Order, notice period and the settlement figures as they are worked out; locked once the settlement is paid through payroll.");
  }
}
