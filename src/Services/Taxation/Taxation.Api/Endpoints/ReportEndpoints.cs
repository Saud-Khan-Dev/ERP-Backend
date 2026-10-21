/// The overview, the reports, the bell's queues, the daily job and the change history.
public class ReportEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attention", async (ISender sender) => (await sender.Send(new GetAttentionQuery())).ToOk())
      .RequireSignedIn()
      .WithTags("Overview").WithName("GetTaxAttention").Produces<AttentionResponse>()
      .WithSummary("Work Waiting")
      .WithDescription("The Taxation queues the caller may act on, each with an exact count (the bell and the overview). Keys: runs.computed, runs.problems, "
        + "notices.pending, notices.unserved, notices.due, demands.overdue, demands.defaulted, yearend.pending, surcharge.due, clearances.expiring, fees.due, "
        + "references.failed, references.pending, sms.queued, ratesets.draft, setup.blocking.");

    app.MapGet("/tax-overview", async (int? fiscalYear, ISender sender) => (await sender.Send(new GetTaxOverviewQuery(fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Overview").WithName("GetTaxOverview").Produces<TaxOverviewDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Taxation Overview")
      .WithDescription("The year at a glance (this year by default): files, demands by status, demanded / recovered / outstanding, by circle, money received month by "
        + "month (taxes and fees), demand by head, notices by stage, fees, clearances, reference syncs and SMS waiting, surcharges, the largest balances, whether "
        + "the in-year surcharge period has passed with no run, and the daily job's last run.");

    var reports = app.MapGroup("/tax-reports").WithTags("Reports");

    reports.MapGet("/collection", async (int fiscalYear, ISender sender) => (await sender.Send(new GetCollectionSummaryQuery(fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetCollectionSummary").Produces<CollectionSummaryDto>()
      .WithSummary("Collection Summary")
      .WithDescription("The year: demanded (this year's charges, arrears billed, surcharges), recovered and the recovery %, by circle, what is outstanding over all years, "
        + "the surcharges on the year's demands and the notices awaiting confirmation.");

    reports.MapGet("/by-head", async (int fiscalYear, Guid? circleId, ISender sender) => (await sender.Send(new GetHeadReportQuery(fiscalYear, circleId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetHeadReport").Produces<HeadReportDto>()
      .WithSummary("By Head").WithDescription("Demanded and recovered per head: this year's charges (of which the annual increase and minimum top-ups), earlier dues billed, surcharges.");

    reports.MapGet("/by-circle-town", async (int fiscalYear, ISender sender) => (await sender.Send(new GetCircleTownReportQuery(fiscalYear))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetCircleTownReport").Produces<CircleTownReportDto>()
      .WithSummary("By Circle and Town").WithDescription("Each town with the circle it was in on 1 July of the year: files, demands, demanded, recovered, outstanding.");

    reports.MapGet("/arrears", async (int? pageIndex, int? pageSize, Guid? circleId, decimal? minAmount, string? search, ISender sender) =>
        (await sender.Send(new GetArrearsReportQuery(QueryParsing.Page(pageIndex, pageSize), circleId, minAmount, search))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetArrearsReport").Produces<ArrearsReportDto>()
      .WithSummary("Arrears").WithDescription("Every file that owes, largest first: the years involved, the last payment, the stage reached, what is outstanding.");

    reports.MapGet("/collection-by-period", async (DateOnly from, DateOnly to, string? groupBy, Guid? circleId, ISender sender) =>
        (await sender.Send(new GetCollectionByPeriodQuery(from, to, groupBy ?? "day", circleId))).ToOk())
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithName("GetCollectionByPeriod").Produces<CollectionByPeriodDto>().ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Collection by Period")
      .WithDescription("Money received from / to (Pakistan time), groupBy = day | month: the dues behind references and fee receipts, and by channel. Reversed payments are left out.");

    // ---- the daily job ----

    app.MapGet("/daily-job", async (IApplicationDbContext context, IConfiguration configuration, CancellationToken cancellationToken) =>
      {
        var runs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
          context.JobRuns.Where(r => r.Job == JobRun.Daily).OrderByDescending(r => r.StartedAt).Take(30), cancellationToken);
        var options = DailyJobOptions.From(configuration);
        return Results.Ok(new DailyJobStatusDto(options.Enabled, options.HourPkt, runs.Select(r => r.ToDto()).ToList()));
      })
      .RequirePermission(PermissionCatalog.Tax.View)
      .WithTags("Overview").WithName("GetDailyJob").Produces<DailyJobStatusDto>()
      .WithSummary("Daily Job").WithDescription("The schedule (hour, Pakistan time) and the last 30 runs: year-end surcharges applied, notices prepared, or the error.");

    app.MapPost("/daily-job/run", async (DailyJob job, ICurrentUser user, CancellationToken cancellationToken) => Results.Ok(await job.RunAsync(user.UserId, cancellationToken)))
      .RequirePermission(PermissionCatalog.Tax.Edit)
      .WithTags("Overview").WithName("RunDailyJob").Produces<JobRunDto>()
      .WithSummary("Run Daily Job Now")
      .WithDescription("The year-end surcharges of years that are over, then the notices due for their next stage (pending confirmation). Safe to run any time.");

    // ---- change history ----

    app.MapGet("/audit-log", async (int? pageIndex, int? pageSize, string? entity, Guid? entityId, Guid? taxFileId, Guid? userId, DateOnly? from, DateOnly? to,
        ISender sender) =>
        (await sender.Send(new GetAuditLogQuery(QueryParsing.Page(pageIndex, pageSize), entity, entityId, taxFileId, userId, from, to))).ToOk())
      .RequireAnyPermission(TaxAuthorization.AnyTaxReader)
      .WithTags("Overview").WithName("GetAuditLog").Produces<GetAuditLogQueryResult>()
      .WithSummary("Change History")
      .WithDescription("Who changed what and when, newest first: entity = the table (tax_head, rate_set, tax_file, payment ...), entityId, taxFileId (a file's history), "
        + "userId, from / to (Pakistan time). oldValues / newValues: the changed fields as JSON.");
  }
}

public sealed record DailyJobStatusDto(bool Enabled, int HourPkt, IReadOnlyList<JobRunDto> Runs);
