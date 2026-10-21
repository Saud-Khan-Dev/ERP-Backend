public sealed record EncroachmentNoticeRequest(string NoticeNo, DateOnly NoticeDate);
public sealed record ResolveEncroachmentRequest(DateOnly ResolutionDate, EncroachmentResolution ResolutionType, string? ResolutionReferenceNo = null, bool OpenRegularization = false);
public sealed record EncroachmentBoundaryRequest(IReadOnlyList<GeoPointInput> Points);

public sealed record RecordHearingRequest(DateOnly HearingDate, string? Proceedings = null, string? OrderPassed = null, DateOnly? NextHearingDate = null,
  string? AttendedBy = null, Guid? AttendedByEmployeeId = null);
public sealed record DecideLitigationRequest(DateOnly DecisionDate, string DecisionOutcome, CaseResult? DecisionResult = null, bool ResolveRelatedEncroachment = false);

public sealed record DecideAppealRequest(DateOnly DecisionDate, AppealOutcome DecisionOutcome, string? DecisionDetails = null);

public sealed record ApproveBuildingPlanRequest(DateOnly ApprovalDate, string? ApprovedBy = null, string? ApprovalReferenceNo = null, DateOnly? ValidityEndDate = null,
  Guid? ApprovedByEmployeeId = null, string? NoDuesCertificateNo = null);

/// Boundaries / GIS and compliance records (schema guide, "Compliance records"): encroachment, litigation,
/// departmental appeals (Act s.32) and building plans.
public class ComplianceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    MapBoundaries(app);
    MapEncroachments(app);
    MapLitigation(app);
    MapAppeals(app);
    MapBuildingPlans(app);
  }

  // =====================================================
  // BOUNDARIES / GIS
  // =====================================================

  private static void MapBoundaries(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/boundaries", async (Guid id, BoundaryInput boundary, ISender sender) =>
        (await sender.Send(new RecordBoundaryCommand(id, boundary))).ToCreated(r => $"/boundaries/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Boundaries").WithName("RecordBoundary")
      .Produces<RecordBoundaryCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Boundary Survey")
      .WithDescription("A polygon of at least three GPS points (latitude / longitude) and the plot's slope in degrees (slopeDegrees, 0-90). Above 45° the survey is recorded and the result carries a Building & Construction Regulations 2026 warning (never a refusal). The latest dated survey is current: a same-day or newer survey supersedes the current boundary (kept as history); an older one is stored as history (isCurrent = false, with a warning). The survey date cannot be in the future.");

    app.MapGet("/properties/{id:guid}/boundaries", async (Guid id, bool? currentOnly, ISender sender) =>
        (await sender.Send(new GetPropertyBoundariesQuery(id, currentOnly ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Boundaries").WithName("GetPropertyBoundaries").Produces<GetBoundariesQueryResult>()
      .WithSummary("Get Boundaries").WithDescription("Points are ordered by sequence number, ready to draw on a map.");

    app.MapGet("/boundaries/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetBoundaryQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Boundaries").WithName("GetBoundary").Produces<GetBoundaryQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Boundary");
  }

  // =====================================================
  // ENCROACHMENT
  // =====================================================

  private static void MapEncroachments(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/encroachments", async (Guid id, EncroachmentInput encroachment, ISender sender) =>
        (await sender.Send(new RecordEncroachmentCommand(id, encroachment))).ToCreated(r => $"/encroachments/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Encroachments").WithName("RecordEncroachment")
      .Produces<RecordEncroachmentCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Encroachment")
      .WithDescription("ENC-00001 is generated; the square-feet area is computed from the unit. Points (optional) are the encroached polygon, kept apart from the property boundary.");

    app.MapGet("/properties/{id:guid}/encroachments", async (Guid id, bool? unresolvedOnly, ISender sender) =>
        (await sender.Send(new GetPropertyEncroachmentsQuery(id, unresolvedOnly ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Encroachments").WithName("GetPropertyEncroachments").Produces<GetEncroachmentsQueryResult>()
      .WithSummary("Get Encroachments");

    var encroachments = app.MapGroup("/encroachments").WithTags("Encroachments");

    encroachments.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetEncroachmentQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetEncroachment").Produces<GetEncroachmentQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Encroachment");

    encroachments.MapPut("/{id:guid}", async (Guid id, EncroachmentDetailsInput details, ISender sender) =>
        (await sender.Send(new UpdateEncroachmentCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateEncroachment").Produces<EncroachmentActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Encroachment").WithDescription("Encroacher and description. The area is fixed once recorded — a different area is a new case.");

    encroachments.MapPost("/{id:guid}/notice", async (Guid id, EncroachmentNoticeRequest request, ISender sender) =>
        (await sender.Send(new IssueEncroachmentNoticeCommand(id, request.NoticeNo, request.NoticeDate))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("IssueEncroachmentNotice").Produces<EncroachmentActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Issue Encroachment Notice").WithDescription("Dated between the detection date and today. An ACTIVE encroachment moves to UNDER_NOTICE.");

    encroachments.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeEncroachmentStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeEncroachmentStatus").Produces<EncroachmentActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Encroachment Status").WithDescription("Under Notice, Under Litigation ...");

    encroachments.MapPost("/{id:guid}/resolve", async (Guid id, ResolveEncroachmentRequest request, ISender sender) =>
        (await sender.Send(new ResolveEncroachmentCommand(id, request.ResolutionDate, request.ResolutionType, request.ResolutionReferenceNo, request.OpenRegularization))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ResolveEncroachment").Produces<EncroachmentActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Resolve Encroachment")
      .WithDescription("An approver closes the case (resolution date not in the future). Removed / Litigated → RESOLVED. Regularized → REGULARIZED; with "
        + "openRegularization=true the area is also recorded as a regularized area case.");

    encroachments.MapPost("/{id:guid}/boundary", async (Guid id, EncroachmentBoundaryRequest request, ISender sender) =>
        (await sender.Send(new SetEncroachmentBoundaryCommand(id, request.Points))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("SetEncroachmentBoundary").Produces<EncroachmentActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Capture Encroached Polygon").WithDescription("Once per encroachment; it is never overwritten.");
  }

  // =====================================================
  // LITIGATION
  // =====================================================

  private static void MapLitigation(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/litigations", async (Guid id, LitigationInput litigation, ISender sender) =>
        (await sender.Send(new FileLitigationCommand(id, litigation))).ToCreated(r => $"/litigations/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Litigation").WithName("FileLitigation")
      .Produces<FileLitigationCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
      .WithSummary("Record Court Case")
      .WithDescription("case_no + court must be unique; a new case starts open (PENDING by default). A criminal complaint is filed only by an authorized officer (Act s.30; 403 otherwise) and is recorded against that signed-in officer (filedByOfficerId is never taken from the request). relatedEncroachmentId moves an unresolved encroachment to UNDER_LITIGATION. With parentLitigationId the case is an appeal: the lower-court case must be DECIDED (not pending, not already appealed) and becomes APPEALED.");

    app.MapGet("/properties/{id:guid}/litigations", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyLitigationsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Litigation").WithName("GetPropertyLitigations").Produces<GetLitigationsQueryResult>()
      .WithSummary("Get Court Cases");

    var litigations = app.MapGroup("/litigations").WithTags("Litigation");

    litigations.MapGet("/upcoming-hearings", async (int? days, ISender sender) =>
        (await sender.Send(new GetUpcomingHearingsQuery(days ?? 30))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetUpcomingHearings").Produces<GetLitigationsQueryResult>()
      .WithSummary("Hearing Diary").WithDescription("Cases with a hearing in the next `days` days (default 30), soonest first.");

    litigations.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetLitigationQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetLitigation").Produces<GetLitigationQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Court Case").WithDescription("The case with its parties and hearing log.");

    litigations.MapPut("/{id:guid}", async (Guid id, LitigationDetailsInput details, ISender sender) =>
        (await sender.Send(new UpdateLitigationCommand(id, details))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateLitigation").Produces<LitigationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Court Case").WithDescription("Open cases only. Changing the type to Criminal Complaint needs an authorized officer (403 otherwise), who becomes the filing officer; the officer of an existing complaint never changes.");

    litigations.MapPost("/{id:guid}/parties", async (Guid id, LitigationPartyInput party, ISender sender) =>
        (await sender.Send(new AddLitigationPartyCommand(id, party))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("AddLitigationParty").Produces<LitigationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Add Party").WithDescription("Open cases only.");

    litigations.MapPost("/{id:guid}/hearings", async (Guid id, RecordHearingRequest request, ISender sender) =>
        (await sender.Send(new RecordHearingCommand(id, request.HearingDate, request.Proceedings, request.OrderPassed, request.NextHearingDate,
          request.AttendedBy, request.AttendedByEmployeeId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("RecordHearing").Produces<LitigationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Hearing").WithDescription("Open cases only; a hearing that took place (not in the future). Its next hearing date becomes the case's "
        + "next hearing date. attendedBy / attendedByEmployeeId: the GDA officer, picked from the staff list.");

    litigations.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeLitigationStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeLitigationStatus").Produces<LitigationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Case Status").WithDescription("Withdrawn, Settled, Closed ... (these close the case and clear its next hearing). Decided goes through decide; Appealed by filing the appeal as a new case. Decided, Appealed, Withdrawn, Settled and Closed cases cannot change.");

    litigations.MapPost("/{id:guid}/decide", async (Guid id, DecideLitigationRequest request, ISender sender) =>
        (await sender.Send(new DecideLitigationCommand(id, request.DecisionDate, request.DecisionOutcome, request.DecisionResult, request.ResolveRelatedEncroachment))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("DecideLitigation").Produces<LitigationActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Decision")
      .WithDescription("An approver records the decision (date not in the future): decisionOutcome (its wording) and decisionResult (InFavourOfGda | AgainstGda | "
        + "PartlyInFavour, required). resolveRelatedEncroachment=true also resolves the encroachment the case is about (Litigated, on the decision date); "
        + "effects lists what changed.");
  }

  // =====================================================
  // DEPARTMENTAL APPEAL (Act s.32)
  // =====================================================

  private static void MapAppeals(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/appeals", async (Guid id, AppealInput appeal, ISender sender) =>
        (await sender.Send(new FileAppealCommand(id, appeal))).ToCreated(r => $"/appeals/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Appeals").WithName("FileAppeal")
      .Produces<FileAppealCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("File Appeal To Chief Secretary")
      .WithDescription("APL-00001 is generated; decisionDueDate = appealDate + 120 days. Filed more than 30 days after the order was received → a warning is returned (rule 6). An order is appealed once: refused while another appeal against the same order (same record, or same order ref) is open or after one was decided. An appeal against a violation marks it APPEALED (a rectified violation cannot be appealed).");

    app.MapGet("/properties/{id:guid}/appeals", async (Guid id, ISender sender) => (await sender.Send(new GetPropertyAppealsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Appeals").WithName("GetPropertyAppeals").Produces<GetAppealsQueryResult>().WithSummary("Get Appeals");

    var appeals = app.MapGroup("/appeals").WithTags("Appeals");

    appeals.MapGet("/overdue", async (ISender sender) => (await sender.Send(new GetOverdueAppealsQuery())).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetOverdueAppeals").Produces<GetAppealsQueryResult>()
      .WithSummary("Overdue Appeals").WithDescription("Open appeals past their 120-day decision date (Act s.32(1)).");

    appeals.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetAppealQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetAppeal").Produces<GetAppealQueryResult>().ProducesProblem(StatusCodes.Status404NotFound).WithSummary("Get Appeal");

    appeals.MapPut("/{id:guid}", async (Guid id, AppealInput appeal, ISender sender) =>
        (await sender.Send(new UpdateAppealCommand(id, appeal))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateAppeal").Produces<AppealActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Appeal").WithDescription("While FILED only. The appealed record (orderSourceTable / orderSourceId) cannot change: withdraw and file a new appeal.");

    appeals.MapPost("/{id:guid}/start-hearing", async (Guid id, ISender sender) => (await sender.Send(new StartAppealHearingCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("StartAppealHearing").Produces<AppealActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Move Appeal To Hearing");

    appeals.MapPost("/{id:guid}/decide", async (Guid id, DecideAppealRequest request, ISender sender) =>
        (await sender.Send(new DecideAppealCommand(id, request.DecisionDate, request.DecisionOutcome, request.DecisionDetails))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("DecideAppeal").Produces<AppealActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Appeal Decision").WithDescription("Allowed / Dismissed / Remanded / Modified; the decision date is not in the future. The decision "
        + "is final (s.32); a late decision returns a warning. On an appealed violation: Allowed sets it aside (RECTIFIED with a remark, an imposed fine "
        + "waived) and reinstates a lease / rental it had cancelled; any other outcome puts it back to its status before the appeal. Allowed against an "
        + "allotment's cancellation restores the allotment (unless another is in force now); against a building plan's rejection puts the plan under "
        + "consideration again. `effects` lists what changed.");

    appeals.MapPost("/{id:guid}/withdraw", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new WithdrawAppealCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("WithdrawAppeal").Produces<AppealActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Withdraw Appeal").WithDescription("An appealed violation goes back to its status before the appeal. A withdrawn appeal does not block a new appeal against the same order.");
  }

  // =====================================================
  // BUILDING PLANS
  // =====================================================

  private static void MapBuildingPlans(IEndpointRouteBuilder app)
  {
    app.MapPost("/properties/{id:guid}/building-plans", async (Guid id, BuildingPlanInput plan, ISender sender) =>
        (await sender.Send(new SubmitBuildingPlanCommand(id, plan))).ToCreated(r => $"/building-plans/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithTags("Building Plans").WithName("SubmitBuildingPlan")
      .Produces<SubmitBuildingPlanCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Submit Building Plan")
      .WithDescription("BP-00001 is generated at revision 0. Upload the drawing as a property document with entityType = BuildingPlan.");

    app.MapGet("/properties/{id:guid}/building-plans", async (Guid id, bool? includeRevisions, ISender sender) =>
        (await sender.Send(new GetPropertyBuildingPlansQuery(id, includeRevisions ?? false))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithTags("Building Plans").WithName("GetPropertyBuildingPlans").Produces<GetBuildingPlansQueryResult>()
      .WithSummary("Get Building Plans").WithDescription("Latest revision of each plan; includeRevisions=true lists every revision.");

    var plans = app.MapGroup("/building-plans").WithTags("Building Plans");

    plans.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetBuildingPlanQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Property.View)
      .WithName("GetBuildingPlan").Produces<GetBuildingPlanQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Building Plan");

    plans.MapPut("/{id:guid}", async (Guid id, BuildingPlanInput plan, ISender sender) =>
        (await sender.Send(new UpdateBuildingPlanCommand(id, plan))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("UpdateBuildingPlan").Produces<BuildingPlanActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Building Plan").WithDescription("While under consideration; an approved, rejected, revised or withdrawn plan changes through a revision.");

    plans.MapPost("/{id:guid}/status", async (Guid id, ChangeMasterStatusRequest request, ISender sender) =>
        (await sender.Send(new ChangeBuildingPlanStatusCommand(id, request.StatusId))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ChangeBuildingPlanStatus").Produces<BuildingPlanActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Change Building Plan Status").WithDescription("Under Review, Withdrawn ... A withdrawn plan is closed like an approved, rejected or revised one: no edit, approval or further status change (submit a revision instead).");

    plans.MapPost("/{id:guid}/approve", async (Guid id, ApproveBuildingPlanRequest request, ISender sender) =>
        (await sender.Send(new ApproveBuildingPlanCommand(id, request.ApprovalDate, request.ApprovedBy, request.ApprovalReferenceNo, request.ValidityEndDate,
          request.ApprovedByEmployeeId, request.NoDuesCertificateNo))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("ApproveBuildingPlan").Produces<BuildingPlanActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve Building Plan")
      .WithDescription("Approval date not in the future. approvedBy / approvedByEmployeeId: the approving officer picked from the staff list (default: the "
        + "signed-in officer). noDuesCertificateNo: the Taxation clearance relied on. warnings: the plot's current survey is steeper than 45 degrees "
        + "(BCR 2026) - approved anyway.");

    plans.MapPost("/{id:guid}/reject", async (Guid id, RemarksRequest request, ISender sender) =>
        (await sender.Send(new RejectBuildingPlanCommand(id, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Property.Approve)
      .WithName("RejectBuildingPlan").Produces<BuildingPlanActionResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Reject Building Plan");

    plans.MapPost("/{id:guid}/revise", async (Guid id, BuildingPlanInput plan, ISender sender) =>
        (await sender.Send(new ReviseBuildingPlanCommand(id, plan))).ToCreated(r => $"/building-plans/{r.Id}"))
      .RequirePermission(PermissionCatalog.Property.Edit)
      .WithName("ReviseBuildingPlan").Produces<ReviseBuildingPlanCommandResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Submit Revised Plan").WithDescription("Same plan number, next revision_no, supersedes_plan_id → this revision (which becomes REVISED).");
  }
}
