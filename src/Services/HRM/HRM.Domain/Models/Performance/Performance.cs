/// An appraisal period (e.g. "PER 2025"). Active periods never overlap.
public class PerformancePeriod : Aggregate<PerformancePeriodId>
{
  public string Name { get; private set; } = default!;
  public DateOnly StartDate { get; private set; }
  public DateOnly EndDate { get; private set; }
  public RecordStatus Status { get; private set; }

  public DateRange Range => new(StartDate, EndDate);

  public static PerformancePeriod Create(PerformancePeriodId id, string name, DateOnly startDate, DateOnly endDate)
  {
    var period = new PerformancePeriod { Id = id, Status = RecordStatus.Active };
    period.Update(name, startDate, endDate);
    return period;
  }

  public void Update(string name, DateOnly startDate, DateOnly endDate)
  {
    Guard.DateOrder(startDate, endDate, "Start date", "End date");
    Name = Guard.RequiredText(name, 100, "Period name");
    StartDate = startDate;
    EndDate = endDate;
  }

  public void SetStatus(RecordStatus status) => Status = status;

  public void EnsureActive()
  {
    if (Status != RecordStatus.Active)
      throw new DomainException($"Performance period '{Name}' is inactive.");
  }
}

public sealed record GoalInput(string Description, decimal Weight, string? Target, string? Achievement, decimal? Score);

public sealed record KpiInput(string KpiName, string? TargetValue, string? AchievedValue, decimal? Score);

public sealed record CompetencyInput(string CompetencyName, decimal? Rating, string? Remarks);

/// The standard PER categories a final score falls in.
public static class PerformanceGrades
{
  public static string ForScore(decimal score) => score switch
  {
    >= 90 => "Outstanding",
    >= 80 => "Very Good",
    >= 65 => "Good",
    >= 50 => "Average",
    _ => "Below Average"
  };
}

/// An employee's appraisal for a period by an evaluator (another employee): draft -> submitted -> acknowledged ->
/// finalized. Goals, KPIs and competencies are edited only while the review is a draft; goal weights must add up to
/// 100 before it is submitted.
public class PerformanceReview : Aggregate<PerformanceReviewId>
{
  private readonly List<PerformanceGoal> _goals = new();
  private readonly List<PerformanceKpi> _kpis = new();
  private readonly List<PerformanceCompetency> _competencies = new();

  public EmployeeId EmployeeId { get; private set; } = default!;
  public PerformancePeriodId PerformancePeriodId { get; private set; } = default!;
  public EmployeeId EvaluatorId { get; private set; } = default!;
  public ReviewStatus Status { get; private set; }
  public decimal? FinalScore { get; private set; }
  public string? PerformanceGrade { get; private set; }
  public bool PromotionRecommended { get; private set; }
  public bool TrainingRecommended { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<PerformanceGoal> Goals => _goals.AsReadOnly();
  public IReadOnlyList<PerformanceKpi> Kpis => _kpis.AsReadOnly();
  public IReadOnlyList<PerformanceCompetency> Competencies => _competencies.AsReadOnly();

  public static PerformanceReview Create(PerformanceReviewId id, Employee employee, PerformancePeriod period, Employee evaluator)
  {
    ArgumentNullException.ThrowIfNull(employee);
    ArgumentNullException.ThrowIfNull(period);
    ArgumentNullException.ThrowIfNull(evaluator);
    period.EnsureActive();
    employee.EnsureProfileActive();
    evaluator.EnsureInService();

    if (employee.Id == evaluator.Id)
      throw new DomainException("An employee cannot evaluate themselves.");

    return new PerformanceReview
    {
      Id = id,
      EmployeeId = employee.Id,
      PerformancePeriodId = period.Id,
      EvaluatorId = evaluator.Id,
      Status = ReviewStatus.Draft
    };
  }

  public void ChangeEvaluator(Employee evaluator)
  {
    EnsureDraft();
    evaluator.EnsureInService();
    if (evaluator.Id == EmployeeId)
      throw new DomainException("An employee cannot evaluate themselves.");
    EvaluatorId = evaluator.Id;
  }

  /// Replaces the goals, KPIs and competencies in one go (the form is saved as a whole).
  public void SetContent(IReadOnlyCollection<GoalInput> goals, IReadOnlyCollection<KpiInput> kpis, IReadOnlyCollection<CompetencyInput> competencies)
  {
    EnsureDraft();

    if (goals.Sum(g => g.Weight) > 100)
      throw new DomainException($"Goal weights add up to {goals.Sum(g => g.Weight):0.##}; they may not exceed 100.");

    // each item keeps its place in the form (SortOrder) - the rows are replaced on every save
    _goals.Clear();
    _goals.AddRange(goals.Select((g, i) => PerformanceGoal.Create(Id, g, i)));
    _kpis.Clear();
    _kpis.AddRange(kpis.Select((k, i) => PerformanceKpi.Create(Id, k, i)));
    _competencies.Clear();
    _competencies.AddRange(competencies.Select((c, i) => PerformanceCompetency.Create(Id, c, i)));
  }

  public void SetRecommendations(bool promotionRecommended, bool trainingRecommended, string? remarks)
  {
    if (Status == ReviewStatus.Finalized)
      throw new DomainException("A finalized review cannot be changed.");

    PromotionRecommended = promotionRecommended;
    TrainingRecommended = trainingRecommended;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }

  public void Submit()
  {
    EnsureDraft();

    var total = _goals.Sum(g => g.Weight);
    if (_goals.Count > 0 && total != 100)
      throw new DomainException($"Goal weights add up to {total:0.##}; they must total 100 before the review is submitted.");

    if (_goals.Count == 0 && _kpis.Count == 0 && _competencies.Count == 0)
      throw new DomainException("Add at least one goal, KPI or competency before submitting the review.");

    Status = ReviewStatus.Submitted;
  }

  /// Sent back to the evaluator for changes.
  public void Reopen()
  {
    if (Status != ReviewStatus.Submitted)
      throw new DomainException("Only a submitted review can be sent back to draft.");

    Status = ReviewStatus.Draft;
  }

  /// The employee has seen the review.
  public void Acknowledge()
  {
    if (Status != ReviewStatus.Submitted)
      throw new DomainException("Only a submitted review can be acknowledged.");

    Status = ReviewStatus.Acknowledged;
  }

  /// Closes the review. The final score is the weighted goal score when every goal is scored; otherwise it is given.
  public void FinalizeReview(decimal? finalScore, string? performanceGrade)
  {
    if (Status is not (ReviewStatus.Submitted or ReviewStatus.Acknowledged))
      throw new DomainException("Only a submitted or acknowledged review can be finalized.");

    var score = finalScore ?? WeightedGoalScore()
      ?? throw new DomainException("Enter the final score: not every goal has a score to work it out from.");

    FinalScore = decimal.Round(Guard.Between(score, 0, 100, "Final score"), 2);
    PerformanceGrade = Guard.Text(performanceGrade, 50, "Grade") ?? PerformanceGrades.ForScore(FinalScore.Value);
    Status = ReviewStatus.Finalized;
  }

  /// Sum of weight x score / 100 over the goals, when every goal has a score.
  public decimal? WeightedGoalScore()
  {
    if (_goals.Count == 0 || _goals.Any(g => g.Score is null))
      return null;

    return decimal.Round(_goals.Sum(g => g.Weight * g.Score!.Value) / 100m, 2);
  }

  /// Only a draft can be removed (one opened for the wrong employee or period).
  public void EnsureDeletable()
  {
    if (Status != ReviewStatus.Draft)
      throw new DomainException($"The review is {EnumText.Words(Status)}; only a draft can be deleted.");
  }

  private void EnsureDraft()
  {
    if (Status != ReviewStatus.Draft)
      throw new DomainException($"The review is {EnumText.Words(Status)}; only a draft can be edited.");
  }
}

public class PerformanceGoal : Entity<PerformanceGoalId>
{
  public PerformanceReviewId PerformanceReviewId { get; private set; } = default!;
  public string Description { get; private set; } = default!;
  public decimal Weight { get; private set; }
  public string? Target { get; private set; }
  public string? Achievement { get; private set; }
  public decimal? Score { get; private set; }
  /// The goal's place on the form (0-based).
  public int SortOrder { get; private set; }

  internal static PerformanceGoal Create(PerformanceReviewId reviewId, GoalInput input, int sortOrder) => new()
  {
    Id = PerformanceGoalId.New(),
    PerformanceReviewId = reviewId,
    SortOrder = sortOrder,
    Description = Guard.RequiredText(input.Description, 2000, "Goal"),
    Weight = Guard.Between(input.Weight, 0, 100, "Goal weight"),
    Target = Guard.Text(input.Target, 2000, "Target"),
    Achievement = Guard.Text(input.Achievement, 2000, "Achievement"),
    Score = Guard.Between(input.Score, 0, 100, "Goal score")
  };
}

public class PerformanceKpi : Entity<PerformanceKpiId>
{
  public PerformanceReviewId PerformanceReviewId { get; private set; } = default!;
  public string KpiName { get; private set; } = default!;
  public string? TargetValue { get; private set; }
  public string? AchievedValue { get; private set; }
  public decimal? Score { get; private set; }
  /// The KPI's place on the form (0-based).
  public int SortOrder { get; private set; }

  internal static PerformanceKpi Create(PerformanceReviewId reviewId, KpiInput input, int sortOrder) => new()
  {
    Id = PerformanceKpiId.New(),
    PerformanceReviewId = reviewId,
    SortOrder = sortOrder,
    KpiName = Guard.RequiredText(input.KpiName, 200, "KPI"),
    TargetValue = Guard.Text(input.TargetValue, 100, "Target value"),
    AchievedValue = Guard.Text(input.AchievedValue, 100, "Achieved value"),
    Score = Guard.Between(input.Score, 0, 100, "KPI score")
  };
}

public class PerformanceCompetency : Entity<PerformanceCompetencyId>
{
  public PerformanceReviewId PerformanceReviewId { get; private set; } = default!;
  public string CompetencyName { get; private set; } = default!;
  public decimal? Rating { get; private set; }
  public string? Remarks { get; private set; }
  /// The competency's place on the form (0-based).
  public int SortOrder { get; private set; }

  internal static PerformanceCompetency Create(PerformanceReviewId reviewId, CompetencyInput input, int sortOrder) => new()
  {
    Id = PerformanceCompetencyId.New(),
    PerformanceReviewId = reviewId,
    SortOrder = sortOrder,
    CompetencyName = Guard.RequiredText(input.CompetencyName, 200, "Competency"),
    Rating = Guard.Between(input.Rating, 0, 10, "Rating"),
    Remarks = Guard.Text(input.Remarks, 2000, "Remarks")
  };
}
