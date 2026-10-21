/// The service rules in force, for screens that must mirror them (maker-checker, retirement, increments, PER weights).
/// Rules GDA still has to confirm are settings with the behaviour the system had before; the screens can say which is used.
public sealed record HrmSettingsDto(
  bool RequireSeparateHrApprover,
  bool RequireSeparatePayrollApprover,
  int RetirementAge,
  bool RetireOnDayBeforeBirthday,
  int AnnualIncrementMonth,
  int AnnualIncrementDay,
  int IncrementQualifyingMonths,
  int PerGoalWeight,
  int PerKpiWeight,
  int PerCompetencyWeight);

public sealed record GetHrmSettingsQuery : IQuery<Result<HrmSettingsDto>>;

public class SettingsHandlers(HrmSettings settings) : IQueryHandler<GetHrmSettingsQuery, Result<HrmSettingsDto>>
{
  public Task<Result<HrmSettingsDto>> Handle(GetHrmSettingsQuery query, CancellationToken cancellationToken)
  {
    var weights = settings.ScoreWeights;
    return Task.FromResult(Result<HrmSettingsDto>.Success(new(settings.RequireSeparateHrApprover, settings.RequireSeparatePayrollApprover, settings.RetirementAge,
      settings.RetireOnDayBeforeBirthday, settings.AnnualIncrementMonth, settings.AnnualIncrementDay, settings.IncrementQualifyingMonths,
      weights.Goals, weights.Kpis, weights.Competencies)));
  }
}
