public sealed record StartReviewsRequest(IReadOnlyList<Guid>? EmployeeIds);
public sealed record OpenReviewRequest(Guid PeriodId, Guid EvaluatorId);
public sealed record ChangeEvaluatorRequest(Guid EvaluatorId);
public sealed record ReviewContentRequest(IReadOnlyList<GoalInput> Goals, IReadOnlyList<KpiInput> Kpis, IReadOnlyList<CompetencyInput> Competencies);
public sealed record ReviewRecommendationsRequest(bool PromotionRecommended, bool TrainingRecommended, string? Remarks);
public sealed record FinalizeReviewRequest(decimal? FinalScore, string? PerformanceGrade);

/// Appraisal periods and performance reviews (PER): draft -> submitted -> acknowledged -> finalized.
public class PerformanceEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    // ---- periods ----

    var periods = app.MapGroup("/performance-periods").WithTags("Performance Periods");

    periods.MapGet("/", async (bool? includeInactive, ISender sender) => (await sender.Send(new GetPerformancePeriodsQuery(includeInactive ?? false))).ToOk())
      .RequireAnyPermission(PermissionCatalog.Hr.View, PermissionCatalog.HrSetup.View)
      .WithName("GetPerformancePeriods")
      .Produces<GetPerformancePeriodsQueryResult>()
      .WithSummary("Get Performance Periods")
      .WithDescription("Newest first, with the number of reviews and how many are finalized. Active periods only unless includeInactive=true.");

    periods.MapPost("/", async (PerformancePeriodInput period, ISender sender) =>
        (await sender.Send(new CreatePerformancePeriodCommand(period))).ToCreated(r => $"/performance-periods/{r.Id}"))
      .RequirePermission(PermissionCatalog.HrSetup.Create)
      .WithName("CreatePerformancePeriod")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Create Performance Period")
      .WithDescription("Active periods may not overlap.");

    periods.MapPut("/{id:guid}", async (Guid id, PerformancePeriodInput period, ISender sender) =>
        (await sender.Send(new UpdatePerformancePeriodCommand(id, period))).ToOk())
      .RequirePermission(PermissionCatalog.HrSetup.Edit)
      .WithName("UpdatePerformancePeriod")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .ProducesProblem(StatusCodes.Status409Conflict)
      .WithSummary("Update Performance Period");

    foreach (var (route, active) in new[] { ("activate", true), ("deactivate", false) })
    {
      periods.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new SetPerformancePeriodActivationCommand(id, active))).ToOk())
        .RequirePermission(PermissionCatalog.HrSetup.Edit)
        .WithName(active ? "ActivatePerformancePeriod" : "DeactivatePerformancePeriod")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary(active ? "Activate Performance Period" : "Deactivate Performance Period")
        .WithDescription(active ? "Refused when it overlaps another active period." : "No new reviews can be opened in it.");
    }

    periods.MapPost("/{id:guid}/start-reviews", async (Guid id, StartReviewsRequest? request, ISender sender) =>
        (await sender.Send(new StartPerformanceReviewsCommand(id, request?.EmployeeIds))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithName("StartPerformanceReviews")
      .Produces<StartPerformanceReviewsResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Open Reviews for Period")
      .WithDescription("A draft review for everyone holding a regular post at the end of the period (or today), or for the employeeIds given. "
        + "The evaluator is the holder of the post's reporting post. Employees whose reporting post is missing, vacant or shared are listed in problems.");

    // ---- reviews ----

    var reviews = app.MapGroup("/performance-reviews").WithTags("Performance Reviews");

    reviews.MapGet("/", async (int? pageIndex, int? pageSize, Guid? periodId, Guid? employeeId, Guid? evaluatorId, string? status, Guid? orgUnitId, ISender sender) =>
        (await sender.Send(new GetPerformanceReviewsQuery(QueryParsing.Page(pageIndex, pageSize), periodId, employeeId, evaluatorId,
          QueryParsing.ParseEnum<ReviewStatus>(status, "status"), orgUnitId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetPerformanceReviews")
      .Produces<GetPerformanceReviewsQueryResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Get Performance Reviews")
      .WithDescription("Newest first. status = draft | submitted | acknowledged | finalized; orgUnitId takes in its sub-units.");

    reviews.MapGet("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new GetPerformanceReviewQuery(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.View)
      .WithName("GetPerformanceReview")
      .Produces<GetPerformanceReviewQueryResult>()
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Get Performance Review")
      .WithDescription("With goals, KPIs and competencies, the goal weight total and the weighted goal score (when every goal is scored).");

    app.MapPost("/employees/{id:guid}/performance-reviews", async (Guid id, OpenReviewRequest request, ISender sender) =>
        (await sender.Send(new CreatePerformanceReviewCommand(id, request.PeriodId, request.EvaluatorId))).ToCreated(r => $"/performance-reviews/{r.Id}"))
      .RequirePermission(PermissionCatalog.Hr.Create)
      .WithTags("Performance Reviews")
      .WithName("CreatePerformanceReview")
      .Produces<CreatedResult>(StatusCodes.Status201Created)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Open Performance Review")
      .WithDescription("One review per employee and period. The evaluator is another employee in service.");

    reviews.MapPut("/{id:guid}/evaluator", async (Guid id, ChangeEvaluatorRequest request, ISender sender) =>
        (await sender.Send(new ChangeReviewEvaluatorCommand(id, request.EvaluatorId))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("ChangeReviewEvaluator")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Change Evaluator")
      .WithDescription("Only while the review is a draft.");

    reviews.MapPut("/{id:guid}/content", async (Guid id, ReviewContentRequest request, ISender sender) =>
        (await sender.Send(new SaveReviewContentCommand(id, request.Goals, request.Kpis, request.Competencies))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("SaveReviewContent")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Save Review Form")
      .WithDescription("Replaces the goals, KPIs and competencies. Only while a draft; goal weights may not exceed 100 (and must total 100 to submit). "
        + "Goal and KPI scores are 0-100, competency ratings 0-10.");

    reviews.MapPut("/{id:guid}/recommendations", async (Guid id, ReviewRecommendationsRequest request, ISender sender) =>
        (await sender.Send(new SetReviewRecommendationsCommand(id, request.PromotionRecommended, request.TrainingRecommended, request.Remarks))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Edit)
      .WithName("SetReviewRecommendations")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Set Recommendations")
      .WithDescription("Promotion and training recommendations and remarks; open until the review is finalized.");

    foreach (var (step, route, summary, description) in new[]
    {
      (ReviewStep.Submit, "submit", "Submit Review", "The evaluator hands the review in."),
      (ReviewStep.Reopen, "reopen", "Send Review Back", "A submitted review goes back to draft for changes."),
      (ReviewStep.Acknowledge, "acknowledge", "Acknowledge Review", "The employee has seen the submitted review (HR records it when signed on paper).")
    })
    {
      reviews.MapPost($"/{{id:guid}}/{route}", async (Guid id, ISender sender) => (await sender.Send(new MoveReviewCommand(id, step))).ToOk())
        .RequirePermission(PermissionCatalog.Hr.Edit)
        .WithName($"{step}Review")
        .Produces<UpdatedResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary(summary)
        .WithDescription(description);
    }

    reviews.MapPost("/{id:guid}/finalize", async (Guid id, FinalizeReviewRequest? request, ISender sender) =>
        (await sender.Send(new FinalizeReviewCommand(id, request?.FinalScore, request?.PerformanceGrade))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Approve)
      .WithName("FinalizeReview")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Finalize Review")
      .WithDescription("A submitted or acknowledged review. Without finalScore it is the weighted goal score; without a grade, the PER category: "
        + "Outstanding (90+), Very Good (80+), Good (65+), Average (50+), Below Average.");

    reviews.MapDelete("/{id:guid}", async (Guid id, ISender sender) => (await sender.Send(new DeletePerformanceReviewCommand(id))).ToOk())
      .RequirePermission(PermissionCatalog.Hr.Delete)
      .WithName("DeletePerformanceReview")
      .Produces<UpdatedResult>()
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status404NotFound)
      .WithSummary("Delete Draft Review");
  }
}
