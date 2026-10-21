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

/// How the final score is made of the parts of the form, in percent (the PER form rules are to be confirmed by GDA:
/// until then the goals alone count, as before).
public sealed record ScoreWeights(int Goals, int Kpis, int Competencies)
{
  public static readonly ScoreWeights GoalsOnly = new(100, 0, 0);
}

/// The standard PER categories a final score falls in.
public static class PerformanceGrades
{
  public static readonly IReadOnlyList<string> All = ["Outstanding", "Very Good", "Good", "Average", "Below Average"];

  public static string Normalize(string? grade) =>
      All.FirstOrDefault(g => string.Equals(g, grade?.Trim(), StringComparison.OrdinalIgnoreCase))
      ?? throw new DomainException($"The grading is one of: {string.Join(", ", All)}.");

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
  /// The countersigning officer (another employee), when the PER has one.
  public EmployeeId? CountersigningOfficerId { get; private set; }
  public string? CountersignRemarks { get; private set; }
  public DateTime? SubmittedAt { get; private set; }
  public DateTime? CountersignedAt { get; private set; }
  public DateTime? AcknowledgedAt { get; private set; }
  public DateTime? FinalizedAt { get; private set; }
  public Guid? FinalizedBy { get; private set; }
  /// Why HR finalized a score different from the one worked out from the form.
  public string? FinalScoreReason { get; private set; }

  /// The employee sees the review only once the reporting officer has submitted it.
  public bool IsVisibleToEmployee => Status != ReviewStatus.Draft;

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

  /// Names (or clears) the countersigning officer, until the PER is countersigned.
  public void SetCountersigningOfficer(Employee? officer)
  {
    if (Status is not (ReviewStatus.Draft or ReviewStatus.Submitted))
      throw new DomainException($"The review is {EnumText.Words(Status)}; the countersigning officer can no longer change.");
    if (officer is not null)
    {
      officer.EnsureInService();
      if (officer.Id == EmployeeId || officer.Id == EvaluatorId)
        throw new DomainException("The countersigning officer must be someone other than the employee and the reporting officer.");
    }
    CountersigningOfficerId = officer?.Id;
  }

  /// The recommendations are part of what the reporting officer submits: they change only while a draft.
  public void SetRecommendations(bool promotionRecommended, bool trainingRecommended, string? remarks)
  {
    EnsureDraft();

    PromotionRecommended = promotionRecommended;
    TrainingRecommended = trainingRecommended;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }

  public void Submit(DateTime? at = null)
  {
    EnsureDraft();
    SubmittedAt = at;

    var total = _goals.Sum(g => g.Weight);
    if (_goals.Count > 0 && total != 100)
      throw new DomainException($"Goal weights add up to {total:0.##}; they must total 100 before the review is submitted.");

    if (_goals.Count == 0 && _kpis.Count == 0 && _competencies.Count == 0)
      throw new DomainException("Add at least one goal, KPI or competency before submitting the review.");

    Status = ReviewStatus.Submitted;
  }

  /// Sent back to the evaluator for changes (before it is countersigned).
  public void Reopen()
  {
    if (Status != ReviewStatus.Submitted)
      throw new DomainException("Only a submitted review that is not countersigned yet can be sent back to draft.");

    Status = ReviewStatus.Draft;
    SubmittedAt = null;
  }

  /// The countersigning officer's part: remarks, and - when they disagree - another grading. `actor` = the employee
  /// countersigning through the portal (must be the named officer); null when HR records it from the paper.
  public void Countersign(EmployeeId? actor, string? remarks, string? grade, DateTime at)
  {
    if (Status != ReviewStatus.Submitted)
      throw new DomainException($"The review is {EnumText.Words(Status)}; only a submitted review is countersigned.");
    if (CountersigningOfficerId is null)
      throw new DomainException("This review has no countersigning officer.");
    if (actor is not null && actor != CountersigningOfficerId)
      throw new DomainException("Only the named countersigning officer can countersign this review.");

    CountersignRemarks = Guard.Text(remarks, 4000, "Countersigning remarks")
      ?? throw new DomainException("Write the countersigning remarks.");
    if (!string.IsNullOrWhiteSpace(grade))
      PerformanceGrade = PerformanceGrades.Normalize(grade);
    CountersignedAt = at;
    Status = ReviewStatus.Countersigned;
  }

  /// The employee has seen the review (after the countersigning officer, when there is one).
  public void Acknowledge(DateTime? at = null)
  {
    var ready = Status == ReviewStatus.Countersigned || (Status == ReviewStatus.Submitted && CountersigningOfficerId is null);
    if (!ready)
      throw new DomainException(Status == ReviewStatus.Submitted
        ? "The countersigning officer has not countersigned the review yet."
        : "Only a submitted review can be acknowledged.");

    Status = ReviewStatus.Acknowledged;
    AcknowledgedAt = at;
  }

  /// The score worked out from the form with the weights in force: null when a part that counts is not scored.
  public decimal? ComputedScore(ScoreWeights weights)
  {
    decimal total = 0;
    if (weights.Goals > 0)
    {
      if (WeightedGoalScore() is not { } goals)
        return null;
      total += goals * weights.Goals;
    }
    if (weights.Kpis > 0)
    {
      if (_kpis.Count == 0 || _kpis.Any(k => k.Score is null))
        return null;
      total += _kpis.Average(k => k.Score!.Value) * weights.Kpis;
    }
    if (weights.Competencies > 0)
    {
      if (_competencies.Count == 0 || _competencies.Any(c => c.Rating is null))
        return null;
      total += _competencies.Average(c => c.Rating!.Value) * 10m * weights.Competencies;
    }
    return decimal.Round(total / 100m, 2);
  }

  /// Closes the review. The final score is the one worked out from the form; a different score needs the reason. The
  /// grading is the countersigning officer's when given, else the given one, else the score's category.
  public void FinalizeReview(decimal? finalScore, string? performanceGrade, ScoreWeights weights, string? reason = null, Guid? finalizedBy = null, DateTime? at = null)
  {
    var ready = Status is ReviewStatus.Acknowledged or ReviewStatus.Countersigned
      || (Status == ReviewStatus.Submitted && CountersigningOfficerId is null);
    if (!ready)
      throw new DomainException(Status == ReviewStatus.Submitted
        ? "The countersigning officer has not countersigned the review yet."
        : "Only a submitted, countersigned or acknowledged review can be finalized.");

    var computed = ComputedScore(weights);
    var score = finalScore ?? computed
      ?? throw new DomainException("Enter the final score: not every part of the form has a score to work it out from.");
    var why = Guard.Text(reason, 1000, "Reason");
    if (computed is { } worked && decimal.Round(score, 2) != worked && why is null)
      throw new DomainException($"The form works out to {worked:0.##}. Say why the final score is different.");

    FinalScore = decimal.Round(Guard.Between(score, 0, 100, "Final score"), 2);
    PerformanceGrade = !string.IsNullOrWhiteSpace(performanceGrade) ? PerformanceGrades.Normalize(performanceGrade)
      : CountersignedAt is not null && PerformanceGrade is not null ? PerformanceGrade
      : PerformanceGrades.ForScore(FinalScore.Value);
    FinalScoreReason = why;
    FinalizedBy = finalizedBy;
    FinalizedAt = at;
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
