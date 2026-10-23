public sealed record ComputeRunRequest(int FiscalYear, Guid? CircleId);
public sealed record ServeNoticeRequest(Guid? ServedByEmployeeId, DateOnly ServedOn, ServiceMode Mode);
public sealed record ConfirmPendingRequest(Guid? CircleId, NoticeStage? Stage);
public sealed record ManualNoticeRequest(
  int FiscalYear,
  NoticeStage Stage,
  DateOnly? IssueDate,
  NoticeOptions? Options,
  IReadOnlyList<HeadModeInput>? HeadModes,
  IReadOnlyList<LineAmountInput>? LineAmounts,
  IReadOnlyList<DuesRowInput>? Rows,
  string? Reason,
  bool IssueNow);

/// The annual demand (runs, drafts, approval) and the notices: prepared, reviewed and confirmed, served, escalated, and
/// created by hand for one property.
public class DemandNoticeEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- demand runs ----

    var runs = app.MapGroup("/demand-runs").WithTags("Demands");

    runs.MapGet("/", async (int? fiscalYear, string? status, ISender sender) =>
        (await sender.Send(new GetDemandRunsQuery(fiscalYear, QueryParsing.ParseEnum<DemandRunStatus>(status, "status")))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetDemandRuns").Produces<GetDemandRunsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Demand Runs").WithDescription("status = computed | approved | cancelled.");

    runs.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetDemandRunQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetDemandRun").Produces<GetDemandRunQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Demand Run").WithDescription("With its demands.");

    runs.MapGet("/{id:guid}/problems", async (Guid id, ISender sender) => (await sender.Send(new GetDemandRunProblemsQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetDemandRunProblems").Produces<GetDemandRunProblemsQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Demand Run Problems")
      .WithDescription("The active files of the run's scope that have no demand of the year and could not be worked out, with why - worked out again now, nothing saved.");

    runs.MapPost("/", async (ComputeRunRequest r, ISender sender) => (await sender.Send(new ComputeDemandRunCommand(r.FiscalYear, r.CircleId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithName("ComputeDemandRun").Produces<ComputeRunResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Compute Demand Run")
      .WithDescription("Drafts the year's demands (fiscalYear 2026 = FY 2026-27) for every active file, all circles or one, with the rate set in force on 1 July. "
        + "Files whose demand of the year is issued are skipped; files on hold are left out; problems (no area, no factors, no payer) are listed. "
        + "A computed run of the same scope is replaced. Year-end surcharges of past years are applied first.");

    runs.MapPost("/{id:guid}/approve", async (Guid id, ISender sender) => (await sender.Send(new ApproveDemandRunCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ApproveDemandRun").Produces<ApproveRunResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Approve Demand Run")
      .WithDescription("Issues the drafts: earlier years' unpaid balances move onto them as arrears by year and head, the first notices are issued with their SMS, "
        + "and every reference is re-synced. The caller is recorded as the approver; while the duty rule TAX_DEMAND_RUN is on, the person who worked the run out "
        + "cannot approve it. A person limited to some towns approves only a run of a circle whose towns are all theirs.");

    runs.MapPost("/{id:guid}/cancel", async (Guid id, ReasonRequest r, ISender sender) => (await sender.Send(new CancelDemandRunCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("CancelDemandRun").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Demand Run").WithDescription("A computed run only; its drafts are removed. reason: why (kept in the audit trail).");

    // ---- demands ----

    var demands = app.MapGroup("/demands").WithTags("Demands");

    demands.MapGet("/", async (int? pageIndex, int? pageSize, int? fiscalYear, Guid? circleId, string? status, string? stage, Guid? runId, Guid? taxFileId, string? search,
        bool? withBalance, bool? overdue, bool? noticeDue, bool? yearEndDue, ISender sender) =>
        (await sender.Send(new GetDemandsQuery(QueryParsing.Page(pageIndex, pageSize), fiscalYear, circleId, QueryParsing.ParseEnum<DemandStatus>(status, "status"),
          QueryParsing.ParseEnum<DemandStage>(stage, "stage"), runId, taxFileId, search, withBalance, overdue, noticeDue, yearEndDue))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetDemands").Produces<GetDemandsQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Demands")
      .WithDescription("status = draft | issued | part_paid | paid | defaulted | carried; stage = first | second | final | show_cause | penalty | sealed. "
        + "withBalance: still owing; overdue: still owing after its due date; noticeDue: its next notice is due today (the bell's count); yearEndDue: a "
        + "year that is over, still owing, its year-end surcharge not applied yet. search matches the demand no., payer, file no. or reference (spaces ignored).");

    demands.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetDemandQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetDemand").Produces<GetDemandQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Demand").WithDescription("Its lines (how each was worked out, paid, outstanding), arrears by year and head, surcharges and notices.");

    // ---- notices ----

    var notices = app.MapGroup("/notices").WithTags("Notices");

    notices.MapGet("/", async (int? pageIndex, int? pageSize, string? status, string? stage, int? fiscalYear, Guid? circleId, DateOnly? issuedOn, bool? served,
        Guid? taxFileId, Guid? demandId, string? search, ISender sender) =>
        (await sender.Send(new GetNoticesQuery(QueryParsing.Page(pageIndex, pageSize), QueryParsing.ParseEnum<NoticeStatus>(status, "status"),
          QueryParsing.ParseEnum<NoticeStage>(stage, "stage"), fiscalYear, circleId, issuedOn, served, taxFileId, demandId, search))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetNotices").Produces<GetNoticesQueryResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Notices")
      .WithDescription("status = pending | issued | cancelled. served=false: issued and not served yet (the Tax Collectors' list for a date with issuedOn).");

    notices.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetNoticeQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetNotice").Produces<GetNoticeQueryResult>().ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Notice (print)")
      .WithDescription("Everything the printed notice shows in the Section's format: letterhead, office, title, To block, paragraph (filled), the table with its columns, "
        + "the payment block (reference, QR payload) and the signature and endorsement. An issued notice shows exactly what was issued.");

    notices.MapPut("/{id:guid}", async (Guid id, NoticeChanges changes, ISender sender) => (await sender.Send(new UpdatePendingNoticeCommand(id, changes))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("UpdatePendingNotice").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Edit Pending Notice")
      .WithDescription("Name, mobile, ID, plot no., town, address line, reply-by (each change needs the reason), payment method, paragraph / table and columns.");

    notices.MapPost("/{id:guid}/confirm", async (Guid id, NoticeChanges? changes, ISender sender) => (await sender.Send(new ConfirmNoticeCommand(id, changes))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("ConfirmNotice").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Confirm and Issue Notice")
      .WithDescription("Freezes the amount behind the reference, the table, the QR and the signing officer (holder of the circle's TO post in HRM), and sends the stage's SMS.");

    notices.MapPost("/confirm-pending", async (ConfirmPendingRequest? r, ISender sender) => (await sender.Send(new ConfirmPendingNoticesCommand(r?.CircleId, r?.Stage))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Approve)
      .WithName("ConfirmPendingNotices").Produces<ConfirmPendingResult>()
      .WithSummary("Issue Pending Notices").WithDescription("Every pending notice dated today or earlier, as prepared (files on hold are left).");

    notices.MapPost("/{id:guid}/cancel", async (Guid id, ReasonRequest r, ISender sender) => (await sender.Send(new CancelNoticeCommand(id, r.Reason))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("CancelNotice").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Cancel Pending Notice").WithDescription("reason: why (kept in the audit trail).");

    notices.MapPost("/{id:guid}/serve", async (Guid id, ServeNoticeRequest r, ISender sender) =>
        (await sender.Send(new ServeNoticeCommand(id, r.ServedByEmployeeId, r.ServedOn, r.Mode))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithName("ServeNotice").Produces<UpdatedResult>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Record Service")
      .WithDescription("servedByEmployeeId: the Tax Collector (HRM employee). mode = by_hand | post | affixed | refused.");

    notices.MapPost("/escalate", async (ISender sender) => (await sender.Send(new EscalateNoticesCommand())).ToOk())
      .RequirePermission(PermissionCatalog.Tax.Run)
      .WithName("EscalateNotices").Produces<EscalateResult>()
      .WithSummary("Prepare Due Notices")
      .WithDescription("For every unpaid demand of a year not yet over: the next of second, final and show cause once its day has come (days from the first notice, "
        + "in working days by the HRM calendar), dated that day and pending confirmation. Safe to run daily. TAX.RUN, and every town.");

    app.MapGet("/tax-files/{id:guid}/notice-context", async (Guid id, int fiscalYear, ISender sender) => (await sender.Send(new GetNoticeContextQuery(id, fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Notices").WithName("GetNoticeContext").Produces<NoticeContextDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("New Notice: Form Context")
      .WithDescription("Whether the year's demand exists (the notice is issued on it) or the notice will create it, the next stage, each head with its Normal / Special "
        + "options, the dues already on record, and the printed fields pre-filled.");

    app.MapPost("/tax-files/{id:guid}/notices", async (Guid id, ManualNoticeRequest r, ISender sender) =>
        (await sender.Send(new CreateManualNoticeCommand(id, r.FiscalYear, r.Stage, r.IssueDate, r.Options ?? new NoticeOptions(), r.HeadModes, r.LineAmounts, r.Rows,
          r.Reason, r.IssueNow))).ToCreated(n => $"/notices/{n.NoticeId}"))
      .RequirePermission(PermissionCatalog.Tax.Create)
      .WithTags("Notices").WithName("CreateManualNotice").Produces<ManualNoticeResult>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("New Notice")
      .WithDescription("headModes: switch a head Normal / Special (policyId); lineAmounts: amounts typed in when the notice creates the demand; rows: earlier years' "
        + "dues and other charges by year and head code (MISC needs a description). Amounts entered or changed need the reason. options: the printed fields "
        + "(name, ID, plot no., town, address line, days, paragraph, reply-by), payVia, sendSms, whether the paragraph and the table print, and the columns. "
        + "issueNow: issue at once, else it waits for confirmation.");
  }
}
