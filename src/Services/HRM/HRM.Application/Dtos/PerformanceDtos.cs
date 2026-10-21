public sealed record PerformancePeriodDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, RecordStatus Status, int Reviews, int Finalized);

public sealed record PerformanceReviewSummaryDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid PerformancePeriodId,
  string? PerformancePeriod,
  Guid EvaluatorId,
  string EvaluatorNumber,
  string EvaluatorName,
  ReviewStatus Status,
  decimal? FinalScore,
  string? PerformanceGrade,
  bool PromotionRecommended,
  bool TrainingRecommended,
  DateTime? UpdatedAt);

public sealed record PerformanceGoalDto(Guid Id, string Description, decimal Weight, string? Target, string? Achievement, decimal? Score);

public sealed record PerformanceKpiDto(Guid Id, string KpiName, string? TargetValue, string? AchievedValue, decimal? Score);

public sealed record PerformanceCompetencyDto(Guid Id, string CompetencyName, decimal? Rating, string? Remarks);

public sealed record PerformanceReviewDto(
  PerformanceReviewSummaryDto Review,
  string? Remarks,
  decimal GoalWeightTotal,
  decimal? WeightedGoalScore,
  IReadOnlyList<PerformanceGoalDto> Goals,
  IReadOnlyList<PerformanceKpiDto> Kpis,
  IReadOnlyList<PerformanceCompetencyDto> Competencies,
  Guid? CountersigningOfficerId = null,
  string? CountersigningOfficerNumber = null,
  string? CountersigningOfficerName = null,
  string? CountersignRemarks = null,
  DateTime? SubmittedAt = null,
  DateTime? CountersignedAt = null,
  DateTime? AcknowledgedAt = null,
  DateTime? FinalizedAt = null,
  Guid? FinalizedBy = null,
  string? FinalScoreReason = null,
  /// The score worked out from the form with the weights in force (null while a part that counts is unscored).
  decimal? ComputedScore = null,
  ScoreWeights? Weights = null);

public static class PerformanceMappings
{
  public static PerformanceReviewDto ToDto(this PerformanceReview x, PerformanceReviewSummaryDto summary, EmployeeName? countersigner = null, ScoreWeights? weights = null) => new(
    summary,
    x.Remarks,
    x.Goals.Sum(g => g.Weight),
    x.WeightedGoalScore(),
    x.Goals.OrderBy(g => g.SortOrder).Select(g => new PerformanceGoalDto(g.Id.Value, g.Description, g.Weight, g.Target, g.Achievement, g.Score)).ToList(),
    x.Kpis.OrderBy(k => k.SortOrder).Select(k => new PerformanceKpiDto(k.Id.Value, k.KpiName, k.TargetValue, k.AchievedValue, k.Score)).ToList(),
    x.Competencies.OrderBy(c => c.SortOrder).Select(c => new PerformanceCompetencyDto(c.Id.Value, c.CompetencyName, c.Rating, c.Remarks)).ToList(),
    x.CountersigningOfficerId?.Value, countersigner?.EmployeeNumber, countersigner?.FullName, x.CountersignRemarks, x.SubmittedAt, x.CountersignedAt,
    x.AcknowledgedAt, x.FinalizedAt, x.FinalizedBy, x.FinalScoreReason, x.ComputedScore(weights ?? ScoreWeights.GoalsOnly), weights ?? ScoreWeights.GoalsOnly);
}
