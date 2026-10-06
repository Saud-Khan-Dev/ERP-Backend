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
  IReadOnlyList<PerformanceCompetencyDto> Competencies);

public static class PerformanceMappings
{
  public static PerformanceReviewDto ToDto(this PerformanceReview x, PerformanceReviewSummaryDto summary) => new(
    summary,
    x.Remarks,
    x.Goals.Sum(g => g.Weight),
    x.WeightedGoalScore(),
    x.Goals.Select(g => new PerformanceGoalDto(g.Id.Value, g.Description, g.Weight, g.Target, g.Achievement, g.Score)).ToList(),
    x.Kpis.Select(k => new PerformanceKpiDto(k.Id.Value, k.KpiName, k.TargetValue, k.AchievedValue, k.Score)).ToList(),
    x.Competencies.Select(c => new PerformanceCompetencyDto(c.Id.Value, c.CompetencyName, c.Rating, c.Remarks)).ToList());
}
