using FluentValidation;
using Microsoft.EntityFrameworkCore;

// ---- periods ----

public sealed record PerformancePeriodInput(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record GetPerformancePeriodsQueryResult(IReadOnlyList<PerformancePeriodDto> Periods);
public sealed record GetPerformancePeriodsQuery(bool IncludeInactive) : IQuery<Result<GetPerformancePeriodsQueryResult>>;
public sealed record CreatePerformancePeriodCommand(PerformancePeriodInput Period) : ICommand<Result<CreatedResult>>;
public sealed record UpdatePerformancePeriodCommand(Guid Id, PerformancePeriodInput Period) : ICommand<Result<UpdatedResult>>;
public sealed record SetPerformancePeriodActivationCommand(Guid Id, bool IsActive) : ICommand<Result<UpdatedResult>>;

public class PerformancePeriodInputValidator : AbstractValidator<PerformancePeriodInput>
{
  public PerformancePeriodInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("The period cannot end before it starts.");
  }
}

public class CreatePerformancePeriodCommandValidator : AbstractValidator<CreatePerformancePeriodCommand>
{
  public CreatePerformancePeriodCommandValidator() => RuleFor(x => x.Period).NotNull().SetValidator(new PerformancePeriodInputValidator());
}

public class UpdatePerformancePeriodCommandValidator : AbstractValidator<UpdatePerformancePeriodCommand>
{
  public UpdatePerformancePeriodCommandValidator() => RuleFor(x => x.Period).NotNull().SetValidator(new PerformancePeriodInputValidator());
}

// ---- reviews ----

/// Who is acting through self-service: the reviewed employee or the evaluator may only reach their own reviews.
public sealed record ReviewActor(Guid EmployeeId, bool AsEvaluator);

public sealed record GetPerformanceReviewsQueryResult(PaginatedResult<PerformanceReviewSummaryDto> Reviews);

public sealed record GetPerformanceReviewsQuery(
  PaginationRequest Pagination,
  Guid? PeriodId,
  Guid? EmployeeId,
  Guid? EvaluatorId,
  ReviewStatus? Status,
  Guid? OrgUnitId) : IQuery<Result<GetPerformanceReviewsQueryResult>>;

public sealed record GetPerformanceReviewQueryResult(PerformanceReviewDto Review);
public sealed record GetPerformanceReviewQuery(Guid Id, ReviewActor? Actor = null) : IQuery<Result<GetPerformanceReviewQueryResult>>;

public sealed record CreatePerformanceReviewCommand(Guid EmployeeId, Guid PeriodId, Guid EvaluatorId) : ICommand<Result<CreatedResult>>;

public sealed record StartPerformanceReviewsResult(int Created, int AlreadyOpen, IReadOnlyList<string> Problems);

/// Opens a draft review for everyone holding a regular post (or the employees given), with the holder of their post's
/// reporting post as the evaluator. Employees who already have a review for the period are skipped.
public sealed record StartPerformanceReviewsCommand(Guid PeriodId, IReadOnlyList<Guid>? EmployeeIds) : ICommand<Result<StartPerformanceReviewsResult>>;

public sealed record ChangeReviewEvaluatorCommand(Guid Id, Guid EvaluatorId) : ICommand<Result<UpdatedResult>>;

/// Saves the form as a whole: goals, KPIs and competencies replace what was there.
public sealed record SaveReviewContentCommand(
  Guid Id,
  IReadOnlyList<GoalInput> Goals,
  IReadOnlyList<KpiInput> Kpis,
  IReadOnlyList<CompetencyInput> Competencies,
  ReviewActor? Actor = null) : ICommand<Result<UpdatedResult>>;

/// The evaluator's or countersigning officer's recommendations; open until the review is finalized.
public sealed record SetReviewRecommendationsCommand(Guid Id, bool PromotionRecommended, bool TrainingRecommended, string? Remarks, ReviewActor? Actor = null)
  : ICommand<Result<UpdatedResult>>;

public enum ReviewStep
{
  Submit,
  Reopen,
  Acknowledge
}

public sealed record MoveReviewCommand(Guid Id, ReviewStep Step, ReviewActor? Actor = null) : ICommand<Result<UpdatedResult>>;

/// Closes the review. Without a score, it is the weighted goal score; without a grade, the PER category of the score.
public sealed record FinalizeReviewCommand(Guid Id, decimal? FinalScore, string? PerformanceGrade) : ICommand<Result<UpdatedResult>>;

public sealed record DeletePerformanceReviewCommand(Guid Id) : ICommand<Result<UpdatedResult>>;

public class SaveReviewContentCommandValidator : AbstractValidator<SaveReviewContentCommand>
{
  public SaveReviewContentCommandValidator()
  {
    RuleFor(x => x.Goals).NotNull().Must(g => g.Count <= 50).WithMessage("At most 50 goals.");
    RuleFor(x => x.Kpis).NotNull().Must(k => k.Count <= 50).WithMessage("At most 50 KPIs.");
    RuleFor(x => x.Competencies).NotNull().Must(c => c.Count <= 50).WithMessage("At most 50 competencies.");
    RuleForEach(x => x.Goals).ChildRules(g =>
    {
      g.RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
      g.RuleFor(x => x.Weight).InclusiveBetween(0, 100);
      g.RuleFor(x => x.Score).InclusiveBetween(0, 100).When(x => x.Score.HasValue);
    });
    RuleForEach(x => x.Kpis).ChildRules(k =>
    {
      k.RuleFor(x => x.KpiName).NotEmpty().MaximumLength(200);
      k.RuleFor(x => x.Score).InclusiveBetween(0, 100).When(x => x.Score.HasValue);
    });
    RuleForEach(x => x.Competencies).ChildRules(c =>
    {
      c.RuleFor(x => x.CompetencyName).NotEmpty().MaximumLength(200);
      c.RuleFor(x => x.Rating).InclusiveBetween(0, 10).When(x => x.Rating.HasValue);
    });
  }
}

public class FinalizeReviewCommandValidator : AbstractValidator<FinalizeReviewCommand>
{
  public FinalizeReviewCommandValidator()
  {
    RuleFor(x => x.FinalScore).InclusiveBetween(0, 100).When(x => x.FinalScore.HasValue);
    RuleFor(x => x.PerformanceGrade).MaximumLength(50);
  }
}

public class PerformanceHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetPerformancePeriodsQuery, Result<GetPerformancePeriodsQueryResult>>,
  ICommandHandler<CreatePerformancePeriodCommand, Result<CreatedResult>>,
  ICommandHandler<UpdatePerformancePeriodCommand, Result<UpdatedResult>>,
  ICommandHandler<SetPerformancePeriodActivationCommand, Result<UpdatedResult>>,
  IQueryHandler<GetPerformanceReviewsQuery, Result<GetPerformanceReviewsQueryResult>>,
  IQueryHandler<GetPerformanceReviewQuery, Result<GetPerformanceReviewQueryResult>>,
  ICommandHandler<CreatePerformanceReviewCommand, Result<CreatedResult>>,
  ICommandHandler<StartPerformanceReviewsCommand, Result<StartPerformanceReviewsResult>>,
  ICommandHandler<ChangeReviewEvaluatorCommand, Result<UpdatedResult>>,
  ICommandHandler<SaveReviewContentCommand, Result<UpdatedResult>>,
  ICommandHandler<SetReviewRecommendationsCommand, Result<UpdatedResult>>,
  ICommandHandler<MoveReviewCommand, Result<UpdatedResult>>,
  ICommandHandler<FinalizeReviewCommand, Result<UpdatedResult>>,
  ICommandHandler<DeletePerformanceReviewCommand, Result<UpdatedResult>>
{
  // ---- periods ----

  public async Task<Result<GetPerformancePeriodsQueryResult>> Handle(GetPerformancePeriodsQuery query, CancellationToken cancellationToken)
  {
    var periods = await context.PerformancePeriods.AsNoTracking()
      .Where(p => query.IncludeInactive || p.Status == RecordStatus.Active)
      .OrderByDescending(p => p.StartDate).ToListAsync(cancellationToken);
    var counts = await context.PerformanceReviews.AsNoTracking()
      .GroupBy(r => r.PerformancePeriodId)
      .Select(g => new { PeriodId = g.Key, Total = g.Count(), Finalized = g.Count(r => r.Status == ReviewStatus.Finalized) })
      .ToDictionaryAsync(x => x.PeriodId, cancellationToken);

    return Result<GetPerformancePeriodsQueryResult>.Success(new(periods.Select(p =>
    {
      var count = counts.GetValueOrDefault(p.Id);
      return new PerformancePeriodDto(p.Id.Value, p.Name, p.StartDate, p.EndDate, p.Status, count?.Total ?? 0, count?.Finalized ?? 0);
    }).ToList()));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePerformancePeriodCommand command, CancellationToken cancellationToken)
  {
    var i = command.Period;
    var period = PerformancePeriod.Create(PerformancePeriodId.New(), i.Name, i.StartDate, i.EndDate);
    if (await context.PerformancePeriods.AnyAsync(p => p.Name.ToLower() == period.Name.ToLower(), cancellationToken))
      return Result<CreatedResult>.Failure($"A performance period named '{period.Name}' already exists.");

    context.PerformancePeriods.Add(period);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(period.Id);
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePerformancePeriodCommand command, CancellationToken cancellationToken)
  {
    var period = await context.LoadPerformancePeriodAsync(command.Id, cancellationToken);
    var i = command.Period;
    period.Update(i.Name, i.StartDate, i.EndDate);
    if (await context.PerformancePeriods.AnyAsync(p => p.Id != period.Id && p.Name.ToLower() == period.Name.ToLower(), cancellationToken))
      return Result<UpdatedResult>.Failure($"Another performance period is named '{period.Name}'.");

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetPerformancePeriodActivationCommand command, CancellationToken cancellationToken)
  {
    var period = await context.LoadPerformancePeriodAsync(command.Id, cancellationToken);
    period.SetStatus(command.IsActive ? RecordStatus.Active : RecordStatus.Inactive);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- reviews ----

  public async Task<Result<GetPerformanceReviewsQueryResult>> Handle(GetPerformanceReviewsQuery query, CancellationToken cancellationToken)
  {
    var rows = context.PerformanceReviews.AsNoTracking();
    if (query.PeriodId is { } period)
    {
      var periodId = PerformancePeriodId.Of(period);
      rows = rows.Where(r => r.PerformancePeriodId == periodId);
    }
    if (query.EmployeeId is { } employee)
    {
      var employeeId = EmployeeId.Of(employee);
      rows = rows.Where(r => r.EmployeeId == employeeId);
    }
    if (query.EvaluatorId is { } evaluator)
    {
      var evaluatorId = EmployeeId.Of(evaluator);
      rows = rows.Where(r => r.EvaluatorId == evaluatorId);
    }
    if (query.Status is { } status)
      rows = rows.Where(r => r.Status == status);
    if (query.OrgUnitId is { } unit)
    {
      var inUnit = await lookup.EmployeesInUnitAsync(OrganizationUnitId.Of(unit), clock.Today, cancellationToken);
      rows = rows.Where(r => inUnit.Contains(r.EmployeeId));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var data = await SummariesAsync(page, cancellationToken);
    return Result<GetPerformanceReviewsQueryResult>.Success(new(new PaginatedResult<PerformanceReviewSummaryDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetPerformanceReviewQueryResult>> Handle(GetPerformanceReviewQuery query, CancellationToken cancellationToken)
  {
    var review = await LoadAsync(query.Id, query.Actor, cancellationToken);
    var summary = (await SummariesAsync([review], cancellationToken))[0];
    return Result<GetPerformanceReviewQueryResult>.Success(new(review.ToDto(summary)));
  }

  public async Task<Result<CreatedResult>> Handle(CreatePerformanceReviewCommand command, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(command.EmployeeId, cancellationToken);
    var evaluator = await context.LoadEmployeeAsync(command.EvaluatorId, cancellationToken);
    var period = await context.LoadPerformancePeriodAsync(command.PeriodId, cancellationToken);
    if (await context.PerformanceReviews.AnyAsync(r => r.EmployeeId == employee.Id && r.PerformancePeriodId == period.Id, cancellationToken))
      return Result<CreatedResult>.Failure($"{employee.DisplayName} already has a review for {period.Name}.");

    var review = PerformanceReview.Create(PerformanceReviewId.New(), employee, period, evaluator);
    context.PerformanceReviews.Add(review);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Created(review.Id);
  }

  public async Task<Result<StartPerformanceReviewsResult>> Handle(StartPerformanceReviewsCommand command, CancellationToken cancellationToken)
  {
    var period = await context.LoadPerformancePeriodAsync(command.PeriodId, cancellationToken);
    period.EnsureActive();

    // where everyone sits at the end of the period (or today, while it runs)
    var date = period.EndDate < clock.Today ? period.EndDate : clock.Today;
    var seats = await (
        from a in context.PositionAssignments
        where a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)
        join v in context.PostVersions on a.PostId equals v.PostId
        where v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date)
        select new { a.EmployeeId, a.PostId, a.AssignmentType, v.ReportingPostId })
      .ToListAsync(cancellationToken);

    var regular = seats.Where(s => s.AssignmentType == AssignmentType.Regular).ToList();
    if (command.EmployeeIds is { Count: > 0 })
    {
      var only = command.EmployeeIds.Select(EmployeeId.Of).ToHashSet();
      regular = regular.Where(s => only.Contains(s.EmployeeId)).ToList();
    }

    var open = (await context.PerformanceReviews.AsNoTracking().Where(r => r.PerformancePeriodId == period.Id)
      .Select(r => r.EmployeeId).ToListAsync(cancellationToken)).ToHashSet();
    var holders = seats.GroupBy(s => s.PostId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.AssignmentType).ToList());

    var employeeIds = regular.Select(s => s.EmployeeId).Concat(seats.Select(s => s.EmployeeId)).Distinct().ToList();
    var employees = await context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
    var postCodes = await lookup.PostCodesAsync(regular.Select(s => s.ReportingPostId), cancellationToken);

    var problems = new List<string>();
    int created = 0, alreadyOpen = 0;
    foreach (var seat in regular.DistinctBy(s => s.EmployeeId))
    {
      var employee = employees[seat.EmployeeId];
      if (open.Contains(seat.EmployeeId))
      {
        alreadyOpen++;
        continue;
      }
      // someone who left after the period still served it and is appraised for it; only a deactivated profile is skipped
      if (employee.ProfileStatus != RecordStatus.Active)
        continue;

      if (seat.ReportingPostId is not { } reportingPost)
      {
        problems.Add($"{employee.EmployeeNumber}: the post has no reporting post; open this review with an evaluator.");
        continue;
      }

      // a regular holder first, else whoever is acting or holds the charge
      var officers = holders.GetValueOrDefault(reportingPost) ?? [];
      var first = officers.FirstOrDefault()?.AssignmentType;
      var candidates = officers.Where(o => o.AssignmentType == first && o.EmployeeId != seat.EmployeeId).DistinctBy(o => o.EmployeeId).ToList();
      var code = postCodes.GetValueOrDefault(reportingPost.Value) ?? "the reporting post";
      if (candidates.Count != 1)
      {
        problems.Add(candidates.Count == 0
          ? $"{employee.EmployeeNumber}: {code} is vacant; open this review with an evaluator."
          : $"{employee.EmployeeNumber}: {code} has {candidates.Count} holders; open this review with an evaluator.");
        continue;
      }

      try
      {
        context.PerformanceReviews.Add(PerformanceReview.Create(PerformanceReviewId.New(), employee, period, employees[candidates[0].EmployeeId]));
        created++;
      }
      catch (DomainException error)
      {
        problems.Add($"{employee.EmployeeNumber}: {DomainMessages.Text(error)}");
      }
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<StartPerformanceReviewsResult>.Success(new(created, alreadyOpen, problems));
  }

  public async Task<Result<UpdatedResult>> Handle(ChangeReviewEvaluatorCommand command, CancellationToken cancellationToken)
  {
    var review = await context.LoadReviewAsync(command.Id, cancellationToken);
    review.ChangeEvaluator(await context.LoadEmployeeAsync(command.EvaluatorId, cancellationToken));
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SaveReviewContentCommand command, CancellationToken cancellationToken)
  {
    var review = await LoadAsync(command.Id, command.Actor, cancellationToken, evaluatorOnly: true);
    review.SetContent(command.Goals, command.Kpis, command.Competencies);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetReviewRecommendationsCommand command, CancellationToken cancellationToken)
  {
    var review = await LoadAsync(command.Id, command.Actor, cancellationToken, evaluatorOnly: true);
    review.SetRecommendations(command.PromotionRecommended, command.TrainingRecommended, command.Remarks);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(MoveReviewCommand command, CancellationToken cancellationToken)
  {
    var review = await LoadAsync(command.Id, command.Actor, cancellationToken);

    // through self-service the evaluator submits and the employee acknowledges; HR may do either and send it back
    if (command.Actor is { } actor && (command.Step == ReviewStep.Reopen
        || (command.Step == ReviewStep.Submit && !actor.AsEvaluator)
        || (command.Step == ReviewStep.Acknowledge && actor.AsEvaluator)))
      return Result<UpdatedResult>.Failure(command.Step switch
      {
        ReviewStep.Submit => "Only the evaluator submits the review.",
        ReviewStep.Acknowledge => "Only the employee reviewed acknowledges the review.",
        _ => "Only HR sends a submitted review back to the evaluator."
      });

    switch (command.Step)
    {
      case ReviewStep.Submit: review.Submit(); break;
      case ReviewStep.Reopen: review.Reopen(); break;
      case ReviewStep.Acknowledge: review.Acknowledge(); break;
    }

    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(FinalizeReviewCommand command, CancellationToken cancellationToken)
  {
    var review = await context.LoadReviewAsync(command.Id, cancellationToken);
    review.FinalizeReview(command.FinalScore, command.PerformanceGrade);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(DeletePerformanceReviewCommand command, CancellationToken cancellationToken)
  {
    var review = await context.LoadReviewAsync(command.Id, cancellationToken);
    review.EnsureDeletable();
    context.PerformanceReviews.Remove(review);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  /// Loads a review; through self-service only the employee's own reviews (or, evaluatorOnly, the ones they evaluate).
  private async Task<PerformanceReview> LoadAsync(Guid id, ReviewActor? actor, CancellationToken cancellationToken, bool evaluatorOnly = false)
  {
    var review = await context.LoadReviewAsync(id, cancellationToken);
    if (actor is null)
      return review;

    var mine = actor.AsEvaluator ? review.EvaluatorId.Value == actor.EmployeeId : review.EmployeeId.Value == actor.EmployeeId;
    if (!mine || (evaluatorOnly && !actor.AsEvaluator))
      throw new PerformanceReviewNotFoundException($"Performance review {id} was not found.");
    return review;
  }

  private async Task<List<PerformanceReviewSummaryDto>> SummariesAsync(List<PerformanceReview> rows, CancellationToken cancellationToken)
  {
    var people = await lookup.EmployeesAsync(rows.SelectMany(r => new EmployeeId?[] { r.EmployeeId, r.EvaluatorId }), cancellationToken);
    var periodIds = rows.Select(r => r.PerformancePeriodId).Distinct().ToList();
    var periods = await context.PerformancePeriods.AsNoTracking().Where(p => periodIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

    return rows.Select(r =>
    {
      var employee = people.GetValueOrDefault(r.EmployeeId.Value);
      var evaluator = people.GetValueOrDefault(r.EvaluatorId.Value);
      return new PerformanceReviewSummaryDto(r.Id.Value, r.EmployeeId.Value, employee?.EmployeeNumber ?? "", employee?.FullName ?? "",
        r.PerformancePeriodId.Value, periods.GetValueOrDefault(r.PerformancePeriodId), r.EvaluatorId.Value, evaluator?.EmployeeNumber ?? "",
        evaluator?.FullName ?? "", r.Status, r.FinalScore, r.PerformanceGrade, r.PromotionRecommended, r.TrainingRecommended, r.UpdatedAt ?? r.CreatedAt);
    }).ToList();
  }
}
